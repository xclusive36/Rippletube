using System.Linq;
using Jellyfin.Plugin.Rippletube.Configuration;
using Jellyfin.Plugin.Rippletube.Models;
using Jellyfin.Plugin.Rippletube.Services;
using Xunit;

namespace Jellyfin.Plugin.Rippletube.Tests;

public sealed class YtDlpArgumentBuilderTests
{
    private readonly YtDlpArgumentBuilder _builder = new();

    [Fact]
    public void CompatibleMp4PresetPrefersMp4WithoutForcedRecode()
    {
        var selector = _builder.GetFormatSelector(DownloadFormatPreset.CompatibleMp4);

        Assert.Contains("bestvideo[ext=mp4]", selector);
        Assert.Contains("bestaudio[ext=m4a]", selector);
        Assert.DoesNotContain("recode", selector);
    }

    [Fact]
    public void SingleVideoAddsNoPlaylistAndSidecars()
    {
        var args = _builder.BuildDownloadArguments(
            new DownloadJob
            {
                Url = "https://example.com/watch?v=abc",
                IsPlaylist = false,
                FormatPreset = DownloadFormatPreset.CompatibleMp4,
                NamingTemplate = NamingTemplatePreset.FlatTitleWithId
            },
            new PluginConfiguration
            {
                DestinationFolder = "/media/downloads",
                FfmpegPath = "/usr/bin/ffmpeg"
            },
            "/state/archive.txt");

        Assert.Contains("--no-playlist", args);
        Assert.Contains("--write-info-json", args);
        Assert.Contains("--write-thumbnail", args);
        Assert.Contains("--download-archive", args);
        Assert.Contains("/state/archive.txt", args);
        Assert.Contains("/usr/bin/ffmpeg", args);
        Assert.DoesNotContain("--ignore-errors", args);
        Assert.Contains("--merge-output-format", args);
        Assert.Contains("mp4", args);
        Assert.Contains(args, item => item.Contains("%(title)s [%(id)s].%(ext)s"));
    }

    [Fact]
    public void PreviewDoesNotRequireFfmpeg()
    {
        var args = _builder.BuildPreviewArguments(
            "https://example.com/watch?v=abc",
            new PluginConfiguration
            {
                FfmpegPath = "/missing/ffmpeg"
            });

        Assert.Contains("--dump-single-json", args);
        Assert.DoesNotContain("--ffmpeg-location", args);
        Assert.DoesNotContain("/missing/ffmpeg", args);
    }

    [Fact]
    public void DownloadLetsYtDlpFindFfmpegCommandOnPath()
    {
        var args = _builder.BuildDownloadArguments(
            new DownloadJob
            {
                Url = "https://example.com/watch?v=abc",
                IsPlaylist = false,
                FormatPreset = DownloadFormatPreset.CompatibleMp4,
                NamingTemplate = NamingTemplatePreset.FlatTitleWithId
            },
            new PluginConfiguration
            {
                DestinationFolder = "/media/downloads",
                FfmpegPath = "ffmpeg"
            },
            "/state/archive.txt");

        Assert.DoesNotContain("--ffmpeg-location", args);
    }

    [Fact]
    public void BestAvailableDoesNotForceMp4MergeContainer()
    {
        var args = _builder.BuildDownloadArguments(
            new DownloadJob
            {
                Url = "https://example.com/watch?v=abc",
                IsPlaylist = false,
                FormatPreset = DownloadFormatPreset.BestAvailable,
                NamingTemplate = NamingTemplatePreset.FlatTitleWithId
            },
            new PluginConfiguration
            {
                DestinationFolder = "/media/downloads",
                FfmpegPath = "ffmpeg"
            },
            "/state/archive.txt");

        Assert.DoesNotContain("--merge-output-format", args);
    }

    [Fact]
    public void PlaylistAddsLimit()
    {
        var args = _builder.BuildDownloadArguments(
            new DownloadJob
            {
                Url = "https://example.com/playlist",
                IsPlaylist = true,
                FormatPreset = DownloadFormatPreset.Capped1080p,
                NamingTemplate = NamingTemplatePreset.PlaylistIndexTitleWithId
            },
            new PluginConfiguration
            {
                DestinationFolder = "/media/downloads",
                FfmpegPath = "ffmpeg",
                MaxPlaylistItems = 12
            },
            "/state/archive.txt");

        var playlistEndIndex = args.ToList().IndexOf("--playlist-end");
        Assert.Contains("--yes-playlist", args);
        Assert.Equal("12", args[playlistEndIndex + 1]);
    }
}
