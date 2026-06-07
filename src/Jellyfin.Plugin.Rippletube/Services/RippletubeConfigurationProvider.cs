using Jellyfin.Plugin.Rippletube.Configuration;

namespace Jellyfin.Plugin.Rippletube.Services;

public interface IRippletubeConfigurationProvider
{
    PluginConfiguration GetConfiguration();
}

public sealed class RippletubeConfigurationProvider : IRippletubeConfigurationProvider
{
    public PluginConfiguration GetConfiguration()
    {
        return Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }
}
