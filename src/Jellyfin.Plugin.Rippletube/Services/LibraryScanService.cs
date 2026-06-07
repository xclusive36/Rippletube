using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Rippletube.Services;

public interface ILibraryScanService
{
    Task TryScanAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Uses reflection to request a Jellyfin library scan without binding v1 to one minor-version method signature.
/// </summary>
public sealed class LibraryScanService : ILibraryScanService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LibraryScanService> _logger;

    public LibraryScanService(IServiceProvider serviceProvider, ILogger<LibraryScanService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task TryScanAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.AutoScanLibrary != true)
        {
            return;
        }

        var libraryManagerType = Type.GetType("MediaBrowser.Controller.Library.ILibraryManager, MediaBrowser.Controller");
        if (libraryManagerType is null)
        {
            _logger.LogWarning("Unable to locate Jellyfin ILibraryManager type; skipping library scan.");
            return;
        }

        var libraryManager = _serviceProvider.GetService(libraryManagerType);
        if (libraryManager is null)
        {
            _logger.LogWarning("Unable to resolve Jellyfin ILibraryManager; skipping library scan.");
            return;
        }

        var scanMethod = libraryManagerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => (method.Name == "ValidateMediaLibrary" || method.Name == "ScanLibrary") && method.GetParameters().Length == 0);

        if (scanMethod is null)
        {
            _logger.LogWarning("Unable to find a compatible Jellyfin library scan method; skipping library scan.");
            return;
        }

        var result = scanMethod.Invoke(libraryManager, null);
        if (result is Task task)
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
