using BBT.Workflow.Execution.Notification;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.Dapr.Metadata;

/// <summary>
/// Background service that warms up Dapr metadata and notification binding resolution during application startup.
/// Retries on failure to ensure metadata is available before processing requests.
/// </summary>
public sealed class DaprMetadataWarmupHostedService : BackgroundService
{
    private readonly IDaprMetadataProvider _metadataProvider;
    private readonly INotificationBindingResolver _notificationResolver;
    private readonly ILogger<DaprMetadataWarmupHostedService> _logger;

    /// <summary>
    /// Default delay between retry attempts.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of retry attempts before giving up.
    /// </summary>
    private const int MaxRetries = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprMetadataWarmupHostedService"/> class.
    /// </summary>
    /// <param name="metadataProvider">The Dapr metadata provider.</param>
    /// <param name="notificationResolver">The notification binding resolver.</param>
    /// <param name="logger">The logger.</param>
    public DaprMetadataWarmupHostedService(
        IDaprMetadataProvider metadataProvider,
        INotificationBindingResolver notificationResolver,
        ILogger<DaprMetadataWarmupHostedService> logger)
    {
        _metadataProvider = metadataProvider;
        _notificationResolver = notificationResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dapr metadata warmup started");

        var retryCount = 0;

        while (!stoppingToken.IsCancellationRequested && retryCount < MaxRetries)
        {
            try
            {
                // 1) Load Dapr metadata snapshot
                var components = await _metadataProvider.GetComponentsAsync(stoppingToken);
                _logger.LogDebug("Loaded {Count} Dapr components", components.Count);

                // 2) Pre-resolve notification binding
                var binding = await _notificationResolver.ResolveAsync(stoppingToken);
                _logger.LogDebug("Resolved notification binding: {Name} ({Kind})", binding.Name, binding.Kind);

                _logger.LogInformation(
                    "Dapr metadata warmup completed successfully. " +
                    "Components: {ComponentCount}, Notification binding: {BindingName} ({BindingKind})",
                    components.Count, binding.Name, binding.Kind);

                return; // Success - exit the loop
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Dapr metadata warmup cancelled");
                return;
            }
            catch (Exception ex)
            {
                retryCount++;

                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(ex,
                        "Dapr metadata warmup failed after {RetryCount} attempts. " +
                        "Notification tasks may not work correctly until Dapr sidecar is available",
                        retryCount);
                    return;
                }

                _logger.LogWarning(ex,
                    "Dapr metadata warmup failed (attempt {RetryCount}/{MaxRetries}). " +
                    "Retrying in {Delay}...",
                    retryCount, MaxRetries, RetryDelay);

                try
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

