using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Rippletube.Configuration;
using Jellyfin.Plugin.Rippletube.Models;
using Jellyfin.Plugin.Rippletube.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Rippletube.Api;

/// <summary>
/// Admin-only Rippletube API.
/// </summary>
[ApiController]
[Route("Rippletube")]
[Authorize(Policy = "RequiresElevation")]
public sealed class RippletubeController : ControllerBase
{
    private readonly IRippletubeQueue _queue;
    private readonly ILogger<RippletubeController> _logger;

    public RippletubeController(IRippletubeQueue queue, ILogger<RippletubeController> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    [HttpGet("Configuration")]
    public ActionResult<PluginConfiguration> GetConfiguration()
    {
        return Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    [HttpPost("Configuration")]
    public ActionResult<ValidationResult> SaveConfiguration([FromBody] SaveConfigurationRequest request)
    {
        var configuration = new PluginConfiguration
        {
            YtDlpPath = request.YtDlpPath.Trim(),
            FfmpegPath = request.FfmpegPath.Trim(),
            DestinationFolder = request.DestinationFolder.Trim(),
            MaxPlaylistItems = request.MaxPlaylistItems,
            MinimumFreeSpaceGb = request.MinimumFreeSpaceGb,
            FormatPreset = request.FormatPreset,
            NamingTemplate = request.NamingTemplate,
            CookiesFilePath = request.CookiesFilePath.Trim(),
            AutoScanLibrary = request.AutoScanLibrary,
            HistoryRetention = request.HistoryRetention
        };

        var result = RippletubeValidator.ValidateConfiguration(configuration);
        if (!result.IsValid)
        {
            return BadRequest(result);
        }

        Plugin.Instance?.UpdateConfiguration(configuration);
        return result;
    }

    [HttpPost("Validate")]
    public Task<ValidationResult> Validate(CancellationToken cancellationToken)
    {
        return _queue.ValidateDependenciesAsync(cancellationToken);
    }

    [HttpPost("Preview")]
    public async Task<ActionResult<DownloadPreview>> Preview([FromBody] PreviewRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _queue.PreviewAsync(request.Url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("Jobs")]
    public async Task<ActionResult<DownloadJob>> Submit([FromBody] SubmitDownloadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _queue.SubmitAsync(request, cancellationToken).ConfigureAwait(false);
            PulseQueueWorker();
            return job;
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("Jobs")]
    public Task<QueueSnapshot> GetJobs(CancellationToken cancellationToken)
    {
        return _queue.GetSnapshotAsync(cancellationToken);
    }

    [HttpPost("Jobs/{jobId:guid}/Cancel")]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken cancellationToken)
    {
        return await _queue.CancelAsync(jobId, cancellationToken).ConfigureAwait(false) ? NoContent() : NotFound();
    }

    [HttpPost("Jobs/{jobId:guid}/Retry")]
    public async Task<ActionResult<DownloadJob>> Retry(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _queue.RetryAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is not null)
        {
            PulseQueueWorker();
        }

        return job is null ? NotFound() : job;
    }

    private void PulseQueueWorker()
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await _queue.RunNextAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start the next Rippletube job.");
                }
            });
    }
}
