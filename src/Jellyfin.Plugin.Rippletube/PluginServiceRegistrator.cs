using Jellyfin.Plugin.Rippletube.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Rippletube;

/// <summary>
/// Registers Rippletube services with Jellyfin.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IProcessRunner, ProcessRunner>();
        serviceCollection.AddSingleton<IRippletubeConfigurationProvider, RippletubeConfigurationProvider>();
        serviceCollection.AddSingleton<IYtDlpArgumentBuilder, YtDlpArgumentBuilder>();
        serviceCollection.AddSingleton<IRippletubeStateStore, RippletubeStateStore>();
        serviceCollection.AddSingleton<ILibraryScanService, LibraryScanService>();
        serviceCollection.AddSingleton<IRippletubeQueue, RippletubeQueue>();
        serviceCollection.AddHostedService<RippletubeWorker>();
    }
}
