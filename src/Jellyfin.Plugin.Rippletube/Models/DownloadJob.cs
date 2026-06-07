using System;

namespace Jellyfin.Plugin.Rippletube.Models;

/// <summary>
/// A durable Rippletube queue job.
/// </summary>
public sealed class DownloadJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Url { get; set; } = string.Empty;

    public bool IsPlaylist { get; set; }

    public DownloadFormatPreset FormatPreset { get; set; }

    public NamingTemplatePreset NamingTemplate { get; set; }

    public DownloadJobStatus Status { get; set; } = DownloadJobStatus.Pending;

    public int ProgressPercent { get; set; }

    public string ProgressText { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Uploader { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    public int? PlaylistCount { get; set; }

    public string OutputPath { get; set; } = string.Empty;

    public string ErrorSummary { get; set; } = string.Empty;

    public int? ExitCode { get; set; }

    public string LogTail { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
