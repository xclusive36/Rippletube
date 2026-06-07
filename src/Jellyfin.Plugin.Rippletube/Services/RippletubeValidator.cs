using System;
using System.IO;
using System.Linq;
using System.Net;
using Jellyfin.Plugin.Rippletube.Configuration;

namespace Jellyfin.Plugin.Rippletube.Services;

/// <summary>
/// Validates Rippletube configuration and submitted URLs.
/// </summary>
public static class RippletubeValidator
{
    public static ValidationResult ValidateConfiguration(PluginConfiguration configuration)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(configuration.YtDlpPath))
        {
            result.Errors.Add("yt-dlp path is required.");
        }

        if (string.IsNullOrWhiteSpace(configuration.FfmpegPath))
        {
            result.Errors.Add("ffmpeg path is required. Use 'ffmpeg' when it is available on PATH.");
        }

        if (string.IsNullOrWhiteSpace(configuration.DestinationFolder))
        {
            result.Errors.Add("Destination folder is required.");
        }
        else if (!Directory.Exists(configuration.DestinationFolder))
        {
            result.Errors.Add("Destination folder does not exist.");
        }

        if (configuration.MaxPlaylistItems < 1 || configuration.MaxPlaylistItems > 500)
        {
            result.Errors.Add("Playlist item cap must be between 1 and 500.");
        }

        if (configuration.MinimumFreeSpaceGb < 0)
        {
            result.Errors.Add("Minimum free space cannot be negative.");
        }

        if (configuration.HistoryRetention < 10 || configuration.HistoryRetention > 1000)
        {
            result.Errors.Add("History retention must be between 10 and 1000 jobs.");
        }

        if (!string.IsNullOrWhiteSpace(configuration.CookiesFilePath) && !File.Exists(configuration.CookiesFilePath))
        {
            result.Errors.Add("Cookies file does not exist.");
        }

        return result;
    }

    public static ValidationResult ValidateUrl(string url)
    {
        var result = new ValidationResult();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            result.Errors.Add("Only absolute HTTP or HTTPS URLs are supported.");
            return result;
        }

        if (IsLocalOrPrivateHost(uri.Host))
        {
            result.Errors.Add("Local and private-network URLs are blocked.");
        }

        return result;
    }

    public static ValidationResult ValidateDiskSpace(PluginConfiguration configuration)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(configuration.DestinationFolder) || !Directory.Exists(configuration.DestinationFolder))
        {
            result.Errors.Add("Destination folder does not exist.");
            return result;
        }

        var root = Path.GetPathRoot(Path.GetFullPath(configuration.DestinationFolder));
        if (string.IsNullOrWhiteSpace(root))
        {
            result.Errors.Add("Unable to determine destination drive.");
            return result;
        }

        var drive = new DriveInfo(root);
        var requiredBytes = configuration.MinimumFreeSpaceGb * 1024L * 1024L * 1024L;
        if (drive.AvailableFreeSpace < requiredBytes)
        {
            result.Errors.Add($"Destination drive has less than {configuration.MinimumFreeSpaceGb} GB free.");
        }

        return result;
    }

    public static bool IsPathWithinDestination(string candidatePath, string destinationFolder)
    {
        var candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var destination = Path.GetFullPath(destinationFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return candidate.Equals(destination, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalOrPrivateHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
               && (bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168)
                   || (bytes[0] == 169 && bytes[1] == 254));
    }
}
