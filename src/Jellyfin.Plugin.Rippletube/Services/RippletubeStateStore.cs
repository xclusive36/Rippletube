using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Rippletube.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Rippletube.Services;

public interface IRippletubeStateStore
{
    string StateDirectory { get; }

    string ArchivePath { get; }

    Task<IReadOnlyList<DownloadJob>> LoadJobsAsync(CancellationToken cancellationToken);

    Task SaveJobsAsync(IReadOnlyList<DownloadJob> jobs, CancellationToken cancellationToken);
}

public sealed class RippletubeStateStore : IRippletubeStateStore
{
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public RippletubeStateStore(IApplicationPaths paths)
    {
        StateDirectory = Path.Combine(paths.DataPath, "rippletube");
        _statePath = Path.Combine(StateDirectory, "queue.json");
        ArchivePath = Path.Combine(StateDirectory, "download-archive.txt");
    }

    public string StateDirectory { get; }

    public string ArchivePath { get; }

    public async Task<IReadOnlyList<DownloadJob>> LoadJobsAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(StateDirectory);
        if (!File.Exists(_statePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_statePath);
        return await JsonSerializer.DeserializeAsync<List<DownloadJob>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false)
               ?? [];
    }

    public async Task SaveJobsAsync(IReadOnlyList<DownloadJob> jobs, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(StateDirectory);
        await using var stream = File.Create(_statePath);
        await JsonSerializer.SerializeAsync(stream, jobs, _jsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
