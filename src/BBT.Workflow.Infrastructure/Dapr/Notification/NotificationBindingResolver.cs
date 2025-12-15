using BBT.Workflow.Execution.Notification;
using BBT.Workflow.Infrastructure.Dapr.Metadata;
using BBT.Workflow.Infrastructure.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Infrastructure.Dapr.Notification;

/// <summary>
/// Resolves the notification binding component from Dapr metadata.
/// Uses AsyncLazy for thread-safe, one-time resolution.
/// </summary>
public sealed class NotificationBindingResolver : INotificationBindingResolver
{
    private readonly IDaprMetadataProvider _metadataProvider;
    private readonly DaprNotificationOptions _options;
    private readonly ILogger<NotificationBindingResolver> _logger;
    private readonly AsyncLazy<NotificationBindingInfo> _lazyBinding;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationBindingResolver"/> class.
    /// </summary>
    /// <param name="metadataProvider">The Dapr metadata provider.</param>
    /// <param name="options">The notification options.</param>
    /// <param name="logger">The logger.</param>
    public NotificationBindingResolver(
        IDaprMetadataProvider metadataProvider,
        IOptions<DaprNotificationOptions> options,
        ILogger<NotificationBindingResolver> logger)
    {
        _metadataProvider = metadataProvider;
        _options = options.Value;
        _logger = logger;
        _lazyBinding = new AsyncLazy<NotificationBindingInfo>(ResolveCoreAsync);
    }

    /// <inheritdoc />
    public Task<NotificationBindingInfo> ResolveAsync(
        CancellationToken cancellationToken = default)
        => _lazyBinding.GetValueAsync(cancellationToken);

    /// <inheritdoc />
    public bool IsResolved => _lazyBinding.IsValueCreated;

    private async Task<NotificationBindingInfo> ResolveCoreAsync(CancellationToken cancellationToken)
    {
        var components = await _metadataProvider.GetComponentsAsync(cancellationToken);
        var targetName = _options.ComponentName;

        var target = components.FirstOrDefault(c =>
            string.Equals(c.Name, targetName, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            throw new InvalidOperationException(
                $"Notification component '{targetName}' not found in Dapr metadata. " +
                $"Available components: {string.Join(", ", components.Select(c => c.Name))}");
        }

        var kind = ClassifyBindingType(target.Type);

        var info = new NotificationBindingInfo(
            name: target.Name,
            type: target.Type,
            version: target.Version,
            kind: kind,
            metadata: target.Metadata);

        _logger.LogInformation(
            "Notification binding resolved. Name: {Name}, Type: {Type}, Kind: {Kind}",
            info.Name, info.Type, info.Kind);

        return info;
    }

    /// <summary>
    /// Classifies the Dapr component type into a <see cref="NotificationBindingKind"/>.
    /// </summary>
    /// <param name="type">The Dapr component type string.</param>
    /// <returns>The classified binding kind.</returns>
    private static NotificationBindingKind ClassifyBindingType(string type)
    {
        var t = type.ToLowerInvariant();

        return t switch
        {
            _ when t.StartsWith("bindings.http") => NotificationBindingKind.Http,
            _ when t.StartsWith("bindings.mqtt") => NotificationBindingKind.Mqtt,
            _ when t.StartsWith("bindings.azure.signalr") => NotificationBindingKind.SignalR,
            _ when t.StartsWith("bindings.signalr") => NotificationBindingKind.SignalR,
            _ when t.StartsWith("bindings.kafka") => NotificationBindingKind.Kafka,
            _ when t.StartsWith("bindings.redis") => NotificationBindingKind.Redis,
            _ when t.StartsWith("bindings.rabbitmq") => NotificationBindingKind.RabbitMq,
            _ => NotificationBindingKind.Unknown
        };
    }
}

