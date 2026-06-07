using System.IO;
using Jellyfin.Plugin.Rippletube.Configuration;
using Jellyfin.Plugin.Rippletube.Services;
using Xunit;

namespace Jellyfin.Plugin.Rippletube.Tests;

public sealed class RippletubeValidatorTests
{
    [Fact]
    public void BlocksLocalUrls()
    {
        var result = RippletubeValidator.ValidateUrl("http://127.0.0.1:8096");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Local", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AllowsPublicHttpsUrls()
    {
        var result = RippletubeValidator.ValidateUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequiresYtDlpFfmpegAndDestination()
    {
        var result = RippletubeValidator.ValidateConfiguration(new PluginConfiguration
        {
            YtDlpPath = "",
            FfmpegPath = "",
            DestinationFolder = ""
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("yt-dlp"));
        Assert.Contains(result.Errors, error => error.Contains("ffmpeg"));
        Assert.Contains(result.Errors, error => error.Contains("Destination"));
    }

    [Fact]
    public void ValidatesPathContainment()
    {
        var root = Path.Combine(Path.GetTempPath(), "rippletube-root");

        Assert.True(RippletubeValidator.IsPathWithinDestination(Path.Combine(root, "child", "file.mp4"), root));
        Assert.False(RippletubeValidator.IsPathWithinDestination(Path.Combine(Path.GetTempPath(), "other", "file.mp4"), root));
    }
}
