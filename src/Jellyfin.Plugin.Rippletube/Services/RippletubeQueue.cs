using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Rippletube.Configuration;
using Jellyfin.Plugin.Rippletube.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Rippletube.Services;

public interface IRippletubeQueue
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<ValidationResult> ValidateDependenciesAsync(CancellationToken cancellationToken);

    Task<DownloadPreview> PreviewAsync(string url, CancellationToken cancellationToken);

    Task<DownloadJob> SubmitAsync(SubmitDownloadRequest request, CancellationToken cancellationToken);

    Task<QueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken);

    Task<DownloadJob?> RetryAsync(Guid jobId, CancellationToken cancellationToken);

    Task RunNextAsync(CancellationToken cancellationToken);
}

public sealed partial class RippletubeQueue : IRippletubeQueue
{
    private readonly IProcessRunner _processRunner;
    private readonly IRippletubeConfigurationProvider _configurationProvider;
    private readonly IYtDlpArgumentBuilder _argumentBuilder;
    private readonly IRippletubeStateStore _stateStore;
    private readonly ILibraryScanService _libraryScanService;
    private readonly ILogger<RippletubeQueue> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<DownloadJob> _jobs = [];
    private CancellationTokenSource? _activeJobCts;
    private bool _initialized;

    public RippletubeQueue(
        IProcessRunner processRunner,
        IRippletubeConfigurationProvider configurationProvider,
        IYtDlpArgumentBuilder argumentBuilder,
        IRippletubeStateStore stateStore,
        ILibraryScanService libraryScanService,
        ILogger<RippletubeQueue> logger)
    {
        _processRunner = processRunner;
        _configurationProvider = configurationProvider;
        _argumentBuilder = argumentBuilder;
        _stateStore = stateStore;
        _libraryScanService = libraryScanService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            _jobs.Clear();
            _jobs.AddRange(await _stateStore.LoadJobsAsync(cancellationToken).ConfigureAwait(false));

            foreach (var job in _jobs.Where(job => job.Status == DownloadJobStatus.Running))
            {
                job.Status = DownloadJobStatus.Failed;
                job.ErrorSummary = "Jellyfin stopped while this job was running.";
                job.FinishedAt = DateTimeOffset.UtcNow;
            }

            TrimHistory();
            await PersistLockedAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ValidationResult> ValidateDependenciesAsync(CancellationToken cancellationToken)
    {
        var configuration = GetConfiguration();
        var result = RippletubeValidator.ValidateConfiguration(configuration);
        if (!result.IsValid)
        {
            return result;
        }

        await ValidateExecutableAsync(configuration.YtDlpPath, "--version", "yt-dlp", result, cancellationToken).ConfigureAwait(false);
        await ValidateExecutableAsync(configuration.FfmpegPath, "-version", "ffmpeg", result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<DownloadPreview> PreviewAsync(string url, CancellationToken cancellationToken)
    {
        EnsureUrl(url);
        var configuration = GetConfiguration();
        var args = _argumentBuilder.BuildPreviewArguments(url, configuration);
        using var previewCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        previewCts.CancelAfter(TimeSpan.FromSeconds(60));

        ProcessRunResult run;
        try
        {
            run = await _processRunner.RunAsync(configuration.YtDlpPath, args, null, null, previewCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("yt-dlp preview timed out after 60 seconds. Check that the Jellyfin server can reach YouTube and that cookies are configured if YouTube requires sign-in.");
        }

        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException(SummarizeProcessFailure(run, "yt-dlp preview failed."));
        }

        using var document = JsonDocument.Parse(run.StandardOutput);
        var root = document.RootElement;

        return new DownloadPreview
        {
            Url = url,
            Title = ReadString(root, "title"),
            Uploader = ReadString(root, "uploader", "channel"),
            Duration = ReadDuration(root),
            ThumbnailUrl = ReadString(root, "thumbnail"),
            PlaylistCount = ReadPlaylistCount(root)
        };
    }

    public async Task<DownloadJob> SubmitAsync(SubmitDownloadRequest request, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        EnsureUrl(request.Url);
        var configuration = GetConfiguration();
        EnsureConfiguration(configuration);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var job = new DownloadJob
            {
                Url = request.Url,
                IsPlaylist = request.IsPlaylist,
                FormatPreset = request.FormatPreset ?? configuration.FormatPreset,
                NamingTemplate = request.NamingTemplate ?? configuration.NamingTemplate,
                Status = DownloadJobStatus.Pending
            };

            _jobs.Insert(0, job);
            TrimHistory();
            await PersistLockedAsync(cancellationToken).ConfigureAwait(false);
            return job;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return new QueueSnapshot { Jobs = _jobs.Select(Clone).ToArray() };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var job = _jobs.FirstOrDefault(item => item.Id == jobId);
            if (job is null)
            {
                return false;
            }

            if (job.Status == DownloadJobStatus.Running)
            {
                _activeJobCts?.Cancel();
                return true;
            }

            if (job.Status == DownloadJobStatus.Pending)
            {
                job.Status = DownloadJobStatus.Canceled;
                job.FinishedAt = DateTimeOffset.UtcNow;
                await PersistLockedAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DownloadJob?> RetryAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var source = _jobs.FirstOrDefault(item => item.Id == jobId);
            if (source is null)
            {
                return null;
            }

            var retry = new DownloadJob
            {
                Url = source.Url,
                IsPlaylist = source.IsPlaylist,
                FormatPreset = source.FormatPreset,
                NamingTemplate = source.NamingTemplate,
                Status = DownloadJobStatus.Pending
            };
            _jobs.Insert(0, retry);
            TrimHistory();
            await PersistLockedAsync(cancellationToken).ConfigureAwait(false);
            return retry;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RunNextAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        DownloadJob? job;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_jobs.Any(item => item.Status == DownloadJobStatus.Running))
            {
                return;
            }

            job = _jobs.LastOrDefault(item => item.Status == DownloadJobStatus.Pending);
            if (job is null)
            {
                return;
            }

            job.Status = DownloadJobStatus.Running;
            job.StartedAt = DateTimeOffset.UtcNow;
            job.ProgressPercent = 0;
            job.ProgressText = "Starting";
            await PersistLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await RunJobAsync(job, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunJobAsync(DownloadJob job, CancellationToken workerToken)
    {
        var configuration = GetConfiguration();
        var validation = RippletubeValidator.ValidateDiskSpace(configuration);
        if (!validation.IsValid)
        {
            await FinishJobAsync(job.Id, DownloadJobStatus.Failed, validation.Errors[0], null, workerToken).ConfigureAwait(false);
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(workerToken);
        _activeJobCts = linkedCts;

        try
        {
            var args = _argumentBuilder.BuildDownloadArguments(job, configuration, _stateStore.ArchivePath);
            var run = await _processRunner.RunAsync(
                configuration.YtDlpPath,
                args,
                line => UpdateProgress(job.Id, line),
                line => UpdateProgress(job.Id, line),
                linkedCts.Token).ConfigureAwait(false);

            var status = run.ExitCode == 0 ? DownloadJobStatus.Completed : DownloadJobStatus.Failed;
            var summary = status == DownloadJobStatus.Completed ? string.Empty : SummarizeProcessFailure(run, "yt-dlp download failed.");
            if (status == DownloadJobStatus.Completed && run.StandardOutput.Contains("has already been recorded in the archive", StringComparison.OrdinalIgnoreCase))
            {
                status = DownloadJobStatus.DuplicateSkipped;
            }

            await FinishJobAsync(job.Id, status, summary, run.ExitCode, workerToken).ConfigureAwait(false);
            if (status == DownloadJobStatus.Completed)
            {
                await _libraryScanService.TryScanAsync(workerToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await FinishJobAsync(job.Id, DownloadJobStatus.Canceled, "Canceled.", null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rippletube job {JobId} failed.", job.Id);
            await FinishJobAsync(job.Id, DownloadJobStatus.Failed, ex.Message, null, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _activeJobCts = null;
        }
    }

    private async Task FinishJobAsync(Guid jobId, DownloadJobStatus status, string errorSummary, int? exitCode, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var job = _jobs.FirstOrDefault(item => item.Id == jobId);
            if (job is null)
            {
                return;
            }

            job.Status = status;
            job.ErrorSummary = errorSummary;
            job.ExitCode = exitCode;
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.ProgressPercent = status == DownloadJobStatus.Completed ? 100 : job.ProgressPercent;
            job.ProgressText = status.ToString();
            TrimHistory();
            await PersistLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void UpdateProgress(Guid jobId, string line)
    {
        var match = ProgressRegex().Match(line);
        if (!match.Success)
        {
            if (line.Contains("[Merger]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[ExtractAudio]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[EmbedThumbnail]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[Metadata]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[Fixup", StringComparison.OrdinalIgnoreCase))
            {
                UpdateJobText(jobId, line, 100);
                return;
            }

            UpdateLogTail(jobId, line);
            return;
        }

        if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent))
        {
            return;
        }

        _gate.Wait();
        try
        {
            var job = _jobs.FirstOrDefault(item => item.Id == jobId);
            if (job is null)
            {
                return;
            }

            job.ProgressPercent = Math.Clamp((int)percent, 0, 100);
            job.ProgressText = line;
            AppendLogTail(job, line);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void UpdateJobText(Guid jobId, string line, int progressPercent)
    {
        _gate.Wait();
        try
        {
            var job = _jobs.FirstOrDefault(item => item.Id == jobId);
            if (job is null)
            {
                return;
            }

            job.ProgressPercent = Math.Clamp(progressPercent, 0, 100);
            job.ProgressText = line;
            AppendLogTail(job, line);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void UpdateLogTail(Guid jobId, string line)
    {
        _gate.Wait();
        try
        {
            var job = _jobs.FirstOrDefault(item => item.Id == jobId);
            if (job is null)
            {
                return;
            }

            AppendLogTail(job, line);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void AppendLogTail(DownloadJob job, string line)
    {
        var combined = string.IsNullOrWhiteSpace(job.LogTail) ? line : job.LogTail + Environment.NewLine + line;
        var lines = combined.Split(Environment.NewLine).TakeLast(30);
        job.LogTail = string.Join(Environment.NewLine, lines);
    }

    private async Task ValidateExecutableAsync(string executable, string versionArgument, string label, ValidationResult result, CancellationToken cancellationToken)
    {
        try
        {
            var run = await _processRunner.RunAsync(executable, [versionArgument], null, null, cancellationToken).ConfigureAwait(false);
            if (run.ExitCode != 0)
            {
                result.Errors.Add($"{label} validation failed: {Summarize(run.StandardError, "non-zero exit")}");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            result.Errors.Add($"{label} was not found or could not be executed: {ex.Message}");
        }
    }

    private PluginConfiguration GetConfiguration()
    {
        return _configurationProvider.GetConfiguration();
    }

    private static void EnsureConfiguration(PluginConfiguration configuration)
    {
        var result = RippletubeValidator.ValidateConfiguration(configuration);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors));
        }
    }

    private static void EnsureUrl(string url)
    {
        var result = RippletubeValidator.ValidateUrl(url);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors));
        }
    }

    private void TrimHistory()
    {
        var retention = Math.Clamp(GetConfiguration().HistoryRetention, 10, 1000);
        var runningOrPending = _jobs.Where(job => job.Status is DownloadJobStatus.Pending or DownloadJobStatus.Running).ToList();
        var history = _jobs.Where(job => job.Status is not (DownloadJobStatus.Pending or DownloadJobStatus.Running))
            .OrderByDescending(job => job.FinishedAt ?? job.CreatedAt)
            .Take(retention)
            .ToList();

        _jobs.Clear();
        _jobs.AddRange(runningOrPending.Concat(history).OrderByDescending(job => job.CreatedAt));
    }

    private Task PersistLockedAsync(CancellationToken cancellationToken)
    {
        return _stateStore.SaveJobsAsync(_jobs, cancellationToken);
    }

    private static DownloadJob Clone(DownloadJob job)
    {
        return new DownloadJob
        {
            Id = job.Id,
            Url = job.Url,
            IsPlaylist = job.IsPlaylist,
            FormatPreset = job.FormatPreset,
            NamingTemplate = job.NamingTemplate,
            Status = job.Status,
            ProgressPercent = job.ProgressPercent,
            ProgressText = job.ProgressText,
            Title = job.Title,
            Uploader = job.Uploader,
            ThumbnailUrl = job.ThumbnailUrl,
            PlaylistCount = job.PlaylistCount,
            OutputPath = job.OutputPath,
            ErrorSummary = job.ErrorSummary,
            ExitCode = job.ExitCode,
            LogTail = job.LogTail,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt
        };
    }

    private static string ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string ReadDuration(JsonElement root)
    {
        if (!root.TryGetProperty("duration", out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return string.Empty;
        }

        var seconds = value.GetInt32();
        return TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
    }

    private static int? ReadPlaylistCount(JsonElement root)
    {
        if (root.TryGetProperty("playlist_count", out var count) && count.ValueKind == JsonValueKind.Number)
        {
            return count.GetInt32();
        }

        if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            return entries.GetArrayLength();
        }

        return null;
    }

    private static string Summarize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var lines = value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.LastOrDefault() ?? fallback;
    }

    private static string SummarizeProcessFailure(ProcessRunResult run, string fallback)
    {
        var combined = string.Join(Environment.NewLine, run.StandardError, run.StandardOutput);
        return Summarize(combined, fallback);
    }

    [GeneratedRegex(@"\[download\]\s+(\d+(?:\.\d+)?)%")]
    private static partial Regex ProgressRegex();
}
