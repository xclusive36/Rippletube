namespace Jellyfin.Plugin.Rippletube.Models;

public sealed class PreviewRequest
{
    public string Url { get; set; } = string.Empty;
}

public sealed class SubmitDownloadRequest
{
    public string Url { get; set; } = string.Empty;

    public bool IsPlaylist { get; set; }

    public DownloadFormatPreset? FormatPreset { get; set; }

    public NamingTemplatePreset? NamingTemplate { get; set; }
}

public sealed class SaveConfigurationRequest
{
    public string YtDlpPath { get; set; } = string.Empty;

    public string FfmpegPath { get; set; } = string.Empty;

    public string DestinationFolder { get; set; } = string.Empty;

    public int MaxPlaylistItems { get; set; }

    public int MinimumFreeSpaceGb { get; set; }

    public DownloadFormatPreset FormatPreset { get; set; }

    public NamingTemplatePreset NamingTemplate { get; set; }

    public string CookiesFilePath { get; set; } = string.Empty;

    public bool AutoScanLibrary { get; set; }

    public int HistoryRetention { get; set; }
}
