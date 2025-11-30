using BBT.Workflow.Caching;

namespace BBT.Workflow.Orchestration.Services;

internal sealed class CacheCleanupHostedService(
    IDomainCacheContext cacheContext,
    ILogger<CacheCleanupHostedService> logger)
    : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _itemTtl = TimeSpan.FromHours(12);
    private readonly int _maxItemsPerSet = 10_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Cache cleanup hosted service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = cacheContext.CleanupAll(
                    ttl: _itemTtl,
                    maxItemsPerSet: _maxItemsPerSet,
                    cancellationToken: stoppingToken);

                if (removed > 0)
                {
                    logger.LogInformation(
                        "Cache cleanup finished. Total removed: {RemovedItems}",
                        removed);
                }
            }
            catch (OperationCanceledException)
            {
                // service stopping
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during cache cleanup.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // service stopping
            }
        }

        logger.LogInformation("Cache cleanup hosted service stopping.");
    }
}