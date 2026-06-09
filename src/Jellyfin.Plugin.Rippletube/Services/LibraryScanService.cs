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
    Task<string> TryScanAsync(CancellationToken cancellationToken);
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

    public async Task<string> TryScanAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.AutoScanLibrary != true)
        {
            return "Automatic Jellyfin library scan is disabled.";
        }

        var libraryManagerType = Type.GetType("MediaBrowser.Controller.Library.ILibraryManager, MediaBrowser.Controller");
        if (libraryManagerType is null)
        {
            _logger.LogWarning("Unable to locate Jellyfin ILibraryManager type; skipping library scan.");
            return "Jellyfin library scan was not queued: ILibraryManager type was unavailable.";
        }

        var libraryManager = _serviceProvider.GetService(libraryManagerType);
        if (libraryManager is null)
        {
            _logger.LogWarning("Unable to resolve Jellyfin ILibraryManager; skipping library scan.");
            return "Jellyfin library scan was not queued: ILibraryManager could not be resolved.";
        }

        try
        {
            var queueMethod = libraryManagerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method => method.Name == "QueueLibraryScan" && method.GetParameters().Length == 0);

            if (queueMethod is not null)
            {
                queueMethod.Invoke(libraryManager, null);
                return "Jellyfin library scan queued.";
            }

            var scanMethod = libraryManagerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method => method.Name == "ValidateMediaLibrary"
                    && method.GetParameters() is var parameters
                    && parameters.Length == 2
                    && parameters[0].ParameterType.IsGenericType
                    && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IProgress<>)
                    && parameters[1].ParameterType == typeof(CancellationToken));

            if (scanMethod is null)
            {
                _logger.LogWarning("Unable to find a compatible Jellyfin library scan method; skipping library scan.");
                return "Jellyfin library scan was not queued: no compatible scan method was found.";
            }

            var progressType = typeof(Progress<>).MakeGenericType(typeof(double));
            var progress = Activator.CreateInstance(progressType);
            var result = scanMethod.Invoke(libraryManager, [progress, cancellationToken]);
            if (result is Task task)
            {
                await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return "Jellyfin library scan completed.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or TargetInvocationException or ArgumentException)
        {
            _logger.LogWarning(ex, "Unable to queue Jellyfin library scan.");
            return $"Jellyfin library scan was not queued: {ex.GetBaseException().Message}";
        }
    }
}
