using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.Rippletube.Configuration;
using Jellyfin.Plugin.Rippletube.Models;

namespace Jellyfin.Plugin.Rippletube.Services;

/// <summary>
/// Builds controlled yt-dlp argument lists.
/// </summary>
public interface IYtDlpArgumentBuilder
{
    IReadOnlyList<string> BuildPreviewArguments(string url, PluginConfiguration configuration);

    IReadOnlyList<string> BuildDownloadArguments(DownloadJob job, PluginConfiguration configuration, string archivePath);

    string GetOutputTemplate(NamingTemplatePreset preset);

    string GetFormatSelector(DownloadFormatPreset preset);
}

/// <inheritdoc />
public sealed class YtDlpArgumentBuilder : IYtDlpArgumentBuilder
{
    /// <inheritdoc />
    public IReadOnlyList<string> BuildPreviewArguments(string url, PluginConfiguration configuration)
    {
        var args = new List<string>
        {
            "--dump-single-json",
            "--no-warnings",
            "--no-progress"
        };

        AppendCookies(args, configuration);
        args.Add(url);
        return args;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> BuildDownloadArguments(DownloadJob job, PluginConfiguration configuration, string archivePath)
    {
        var args = new List<string>
        {
            "--newline",
            "--ignore-errors",
            "--no-overwrites",
            "--download-archive",
            archivePath,
            "--write-info-json",
            "--write-thumbnail",
            "--write-description",
            "--write-subs",
            "--sub-langs",
            "all,-live_chat",
            "--convert-thumbnails",
            "jpg",
            "--embed-metadata",
            "--restrict-filenames",
            "-f",
            GetFormatSelector(job.FormatPreset),
            "-o",
            Path.Combine(configuration.DestinationFolder, GetOutputTemplate(job.NamingTemplate))
        };

        AppendCommonPaths(args, configuration);
        AppendCookies(args, configuration);

        if (!job.IsPlaylist)
        {
            args.Add("--no-playlist");
        }
        else
        {
            args.Add("--yes-playlist");
            args.Add("--playlist-end");
            args.Add(Math.Max(1, configuration.MaxPlaylistItems).ToString(CultureInfo.InvariantCulture));
        }

        args.Add(job.Url);
        return args;
    }

    /// <inheritdoc />
    public string GetOutputTemplate(NamingTemplatePreset preset)
    {
        return preset switch
        {
            NamingTemplatePreset.PlaylistIndexTitleWithId => "%(playlist_title)s/%(playlist_index)03d - %(title)s [%(id)s].%(ext)s",
            NamingTemplatePreset.FlatTitleWithId => "%(title)s [%(id)s].%(ext)s",
            _ => "%(uploader)s/%(title)s [%(id)s]/%(title)s.%(ext)s"
        };
    }

    /// <inheritdoc />
    public string GetFormatSelector(DownloadFormatPreset preset)
    {
        return preset switch
        {
            DownloadFormatPreset.BestAvailable => "bestvideo+bestaudio/best",
            DownloadFormatPreset.AudioOnly => "bestaudio/best",
            DownloadFormatPreset.Capped1080p => "bestvideo[height<=1080]+bestaudio/best[height<=1080]/best",
            _ => "bestvideo[ext=mp4][vcodec^=avc1]+bestaudio[ext=m4a]/best[ext=mp4]/best"
        };
    }

    private static void AppendCommonPaths(List<string> args, PluginConfiguration configuration)
    {
        if (ShouldPassFfmpegLocation(configuration.FfmpegPath))
        {
            args.Add("--ffmpeg-location");
            args.Add(configuration.FfmpegPath);
        }
    }

    private static void AppendCookies(List<string> args, PluginConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.CookiesFilePath))
        {
            args.Add("--cookies");
            args.Add(configuration.CookiesFilePath);
        }
    }

    private static bool ShouldPassFfmpegLocation(string ffmpegPath)
    {
        return !string.IsNullOrWhiteSpace(ffmpegPath)
               && (Path.IsPathFullyQualified(ffmpegPath)
                   || ffmpegPath.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                   || ffmpegPath.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }
}
