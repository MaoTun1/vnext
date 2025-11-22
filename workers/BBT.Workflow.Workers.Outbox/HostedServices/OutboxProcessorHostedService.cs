using BBT.Aether.Events;
using BBT.Workflow.Workers.Outbox.Services;

namespace BBT.Workflow.Workers.Outbox.HostedServices;

public sealed class OutboxProcessorHostedService(
    IMultiSchemaOutboxProcessor processor,
    ILogger<OutboxProcessorHostedService> logger,
    AetherOutboxOptions options)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Outbox Processor Worker starting. Processing interval: {Interval}",
            options.ProcessingInterval);

        // Wait a bit before starting to allow other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processor.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                logger.LogInformation("Outbox processing cancelled");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during outbox processing cycle");
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

        logger.LogInformation("Outbox Processor Worker stopped");
    }
}