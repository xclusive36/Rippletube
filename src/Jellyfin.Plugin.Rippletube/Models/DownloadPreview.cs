namespace Jellyfin.Plugin.Rippletube.Models;

/// <summary>
/// Safe metadata preview returned before a job is submitted.
/// </summary>
public sealed class DownloadPreview
{
    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Uploader { get; set; } = string.Empty;

    public string Duration { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    public int? PlaylistCount { get; set; }

    public bool LooksLikePlaylist => PlaylistCount.GetValueOrDefault() > 1;
}
