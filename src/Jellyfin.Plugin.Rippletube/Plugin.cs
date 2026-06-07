using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Rippletube.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Rippletube;

/// <summary>
/// Rippletube plugin entry point.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Stable plugin id.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("8f3d0a10-5ac8-4b6d-9983-b3495ebd2d81");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public override string Name => "Rippletube";

    /// <inheritdoc />
    public override string Description => "Admin-only yt-dlp downloader and Jellyfin library importer.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "rippletube",
                EmbeddedResourcePath = GetType().Namespace + ".Web.rippletube.html"
            },
            new PluginPageInfo
            {
                Name = "rippletube.js",
                EmbeddedResourcePath = GetType().Namespace + ".Web.rippletube.js"
            },
            new PluginPageInfo
            {
                Name = "rippletube.css",
                EmbeddedResourcePath = GetType().Namespace + ".Web.rippletube.css"
            }
        };
    }
}
