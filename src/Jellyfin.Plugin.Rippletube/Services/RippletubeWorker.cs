using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Rippletube.Services;

/// <summary>
/// Background worker that drains the single download queue.
/// </summary>
public sealed class RippletubeWorker : BackgroundService
{
    private readonly IRippletubeQueue _queue;
    private readonly ILogger<RippletubeWorker> _logger;

    public RippletubeWorker(IRippletubeQueue queue, ILogger<RippletubeWorker> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _queue.InitializeAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _queue.RunNextAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rippletube worker loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
        }
    }
}
