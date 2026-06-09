using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Rippletube.Models;
using Jellyfin.Plugin.Rippletube.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Rippletube.Tests;

public sealed class RippletubeQueueTests
{
    [Fact]
    public async Task SubmitAddsPendingJob()
    {
        var queue = CreateQueue();

        var job = await queue.SubmitAsync(new SubmitDownloadRequest
        {
            Url = "https://example.com/video",
            IsPlaylist = false
        }, CancellationToken.None);

        var snapshot = await queue.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(DownloadJobStatus.Pending, job.Status);
        Assert.Contains(snapshot.Jobs, item => item.Id == job.Id);
    }

    [Fact]
    public async Task CancelPendingJobMarksCanceled()
    {
        var queue = CreateQueue();
        var job = await queue.SubmitAsync(new SubmitDownloadRequest
        {
            Url = "https://example.com/video"
        }, CancellationToken.None);

        var canceled = await queue.CancelAsync(job.Id, CancellationToken.None);
        var snapshot = await queue.GetSnapshotAsync(CancellationToken.None);

        Assert.True(canceled);
        Assert.Contains(snapshot.Jobs, item => item.Id == job.Id && item.Status == DownloadJobStatus.Canceled);
    }

    [Fact]
    public async Task PreviewIncludesStdoutWhenYtDlpFailsWithoutStderr()
    {
        var queue = CreateQueue(new FakeProcessRunner(new ProcessRunResult(1, "yt-dlp stdout failure", string.Empty)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            queue.PreviewAsync("https://example.com/video", CancellationToken.None));

        Assert.Contains("yt-dlp stdout failure", ex.Message);
    }

    [Fact]
    public async Task CompletedDownloadStoresOutputPathAndQueuesLibraryScan()
    {
        var destinationFolder = Path.Combine(Path.GetTempPath(), "rippletube-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destinationFolder);
        var outputPath = Path.Combine(destinationFolder, "Creator", "Video [abc]", "Video.mp4");
        var scanService = new RecordingLibraryScanService();
        var queue = CreateQueue(
            new FakeProcessRunner(new ProcessRunResult(0, outputPath, string.Empty)),
            destinationFolder,
            scanService);

        await queue.SubmitAsync(new SubmitDownloadRequest
        {
            Url = "https://example.com/video",
            IsPlaylist = false
        }, CancellationToken.None);
        await queue.RunNextAsync(CancellationToken.None);

        var snapshot = await queue.GetSnapshotAsync(CancellationToken.None);
        var job = Assert.Single(snapshot.Jobs);

        Assert.Equal(DownloadJobStatus.Completed, job.Status);
        Assert.Equal(outputPath, job.OutputPath);
        Assert.Contains("Jellyfin library scan queued.", job.ProgressText);
        Assert.Equal(1, scanService.CallCount);
    }

    private static RippletubeQueue CreateQueue(
        IProcessRunner? processRunner = null,
        string? destinationFolder = null,
        ILibraryScanService? libraryScanService = null)
    {
        return new RippletubeQueue(
            processRunner ?? new FakeProcessRunner(),
            new FakeConfigurationProvider(destinationFolder),
            new YtDlpArgumentBuilder(),
            new MemoryStateStore(),
            libraryScanService ?? new NoopLibraryScanService(),
            NullLogger<RippletubeQueue>.Instance);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly ProcessRunResult _result;

        public FakeProcessRunner()
            : this(new ProcessRunResult(0, "2026.01.01", string.Empty))
        {
        }

        public FakeProcessRunner(ProcessRunResult result)
        {
            _result = result;
        }

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            Action<string>? onOutput,
            Action<string>? onError,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeConfigurationProvider : IRippletubeConfigurationProvider
    {
        private readonly string _destinationFolder;

        public FakeConfigurationProvider(string? destinationFolder)
        {
            _destinationFolder = destinationFolder ?? Path.GetTempPath();
        }

        public Jellyfin.Plugin.Rippletube.Configuration.PluginConfiguration GetConfiguration()
        {
            return new Jellyfin.Plugin.Rippletube.Configuration.PluginConfiguration
            {
                YtDlpPath = "yt-dlp",
                FfmpegPath = "ffmpeg",
                DestinationFolder = _destinationFolder,
                MaxPlaylistItems = 25,
                MinimumFreeSpaceGb = 0,
                HistoryRetention = 100
            };
        }
    }

    private sealed class MemoryStateStore : IRippletubeStateStore
    {
        private List<DownloadJob> _jobs = [];

        public string StateDirectory => "/tmp/rippletube";

        public string ArchivePath => "/tmp/rippletube/archive.txt";

        public Task<IReadOnlyList<DownloadJob>> LoadJobsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DownloadJob>>(_jobs);
        }

        public Task SaveJobsAsync(IReadOnlyList<DownloadJob> jobs, CancellationToken cancellationToken)
        {
            _jobs = [.. jobs];
            return Task.CompletedTask;
        }
    }

    private sealed class NoopLibraryScanService : ILibraryScanService
    {
        public Task<string> TryScanAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Empty);
        }
    }

    private sealed class RecordingLibraryScanService : ILibraryScanService
    {
        public int CallCount { get; private set; }

        public Task<string> TryScanAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult("Jellyfin library scan queued.");
        }
    }
}
