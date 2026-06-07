namespace Jellyfin.Plugin.Rippletube.Models;

/// <summary>
/// Download job lifecycle states.
/// </summary>
public enum DownloadJobStatus
{
    Pending,
    Previewed,
    Running,
    Completed,
    Failed,
    Canceled,
    DuplicateSkipped
}
