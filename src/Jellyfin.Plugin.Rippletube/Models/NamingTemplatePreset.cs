namespace Jellyfin.Plugin.Rippletube.Models;

/// <summary>
/// Supported safe output path templates.
/// </summary>
public enum NamingTemplatePreset
{
    /// <summary>
    /// Uploader/Title [id]/Title.ext.
    /// </summary>
    UploaderTitleWithId,

    /// <summary>
    /// Playlist/Index - Title [id].ext.
    /// </summary>
    PlaylistIndexTitleWithId,

    /// <summary>
    /// Title [id].ext.
    /// </summary>
    FlatTitleWithId
}
