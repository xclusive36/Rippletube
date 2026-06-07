using System.Collections.Generic;

namespace Jellyfin.Plugin.Rippletube.Models;

/// <summary>
/// Queue state returned to the web UI.
/// </summary>
public sealed class QueueSnapshot
{
    public IReadOnlyList<DownloadJob> Jobs { get; set; } = [];
}
