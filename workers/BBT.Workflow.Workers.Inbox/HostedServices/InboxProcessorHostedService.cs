using BBT.Aether.Events;
using BBT.Workflow.Workers.Inbox.Services;

namespace BBT.Workflow.Workers.Inbox.HostedServices;

public sealed class InboxProcessorHostedService(
    IMultiSchemaInboxProcessor processor,
    ILogger<InboxProcessorHostedService> logger,
    AetherOutboxOptions options)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Inbox Processor Worker starting. Processing interval: {Interval}",
            options.ProcessingInterval);

        // Wait a bit before starting to allow other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processor.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                logger.LogInformation("Inbox cancelled");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during inbox cycle");
                // Continue processing in next iteration
            }

            try
            {
                await Task.Delay(options.ProcessingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
        }

        logger.LogInformation("Inbox Processor Worker stopped");
    }
}
