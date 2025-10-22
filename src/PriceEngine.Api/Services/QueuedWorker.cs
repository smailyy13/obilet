using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceEngine.Api.Data;
using PriceEngine.Api.Models;

namespace PriceEngine.Api.Services;

public sealed class QueuedWorker(
    IBackgroundTaskQueue queue,
    ILogger<QueuedWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var work = await queue.DequeueAsync(stoppingToken);
                await work(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background worker crash.");
                await Task.Delay(1000, stoppingToken);
            }
        }
        logger.LogInformation("Background worker stopping.");
    }
}
