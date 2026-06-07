namespace Jellyfin.Plugin.Rippletube.Models;

/// <summary>
/// Supported safe yt-dlp format presets.
/// </summary>
public enum DownloadFormatPreset
{
    /// <summary>
    /// Prefer Jellyfin-friendly MP4/H.264/AAC without forcing full recoding.
    /// </summary>
    CompatibleMp4,

    /// <summary>
    /// Download the best available format.
    /// </summary>
    BestAvailable,

    /// <summary>
    /// Download audio only.
    /// </summary>
    AudioOnly,

    /// <summary>
    /// Cap video height at 1080p.
    /// </summary>
    Capped1080p
}
