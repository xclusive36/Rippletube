using Jellyfin.Plugin.Rippletube.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Rippletube.Configuration;

/// <summary>
/// Rippletube plugin configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the yt-dlp executable path or command.
    /// </summary>
    public string YtDlpPath { get; set; } = "yt-dlp";

    /// <summary>
    /// Gets or sets the ffmpeg executable path or command. Empty means yt-dlp will use PATH.
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>
    /// Gets or sets the destination folder. It should already be part of a Jellyfin library.
    /// </summary>
    public string DestinationFolder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of playlist items per job.
    /// </summary>
    public int MaxPlaylistItems { get; set; } = 25;

    /// <summary>
    /// Gets or sets the minimum free disk space required before a job starts, in gigabytes.
    /// </summary>
    public int MinimumFreeSpaceGb { get; set; } = 5;

    /// <summary>
    /// Gets or sets the safe format preset.
    /// </summary>
    public DownloadFormatPreset FormatPreset { get; set; } = DownloadFormatPreset.CompatibleMp4;

    /// <summary>
    /// Gets or sets the output naming preset.
    /// </summary>
    public NamingTemplatePreset NamingTemplate { get; set; } = NamingTemplatePreset.UploaderTitleWithId;

    /// <summary>
    /// Gets or sets an optional server-side cookies file path.
    /// </summary>
    public string CookiesFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether Jellyfin should scan libraries after successful downloads.
    /// </summary>
    public bool AutoScanLibrary { get; set; } = true;

    /// <summary>
    /// Gets or sets how many completed or failed jobs to retain.
    /// </summary>
    public int HistoryRetention { get; set; } = 100;
}
