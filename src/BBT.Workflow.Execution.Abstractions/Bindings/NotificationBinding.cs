namespace BBT.Workflow.Execution.Bindings;

/// <summary>
/// Binding configuration for notification task execution via Dapr binding.
/// </summary>
public sealed class NotificationBinding
{
    /// <summary>
    /// The name of the Dapr binding component to use.
    /// If not specified, the resolver will use the configured default component.
    /// </summary>
    public string? BindingName { get; init; }

    /// <summary>
    /// The operation to perform on the binding (e.g., "create", "post").
    /// Default is "create" for most bindings.
    /// </summary>
    public string Operation { get; init; } = "create";

    /// <summary>
    /// Request body/data as JSON string.
    /// Contains the notification payload to send.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Target recipients or destination for the notification.
    /// Interpretation depends on binding type (email addresses, user IDs, topics, etc.).
    /// </summary>
    public string[]? To { get; init; }

    /// <summary>
    /// Notification subject or title.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Additional metadata for the binding invocation.
    /// Keys and values depend on the specific binding type being used.
    /// </summary>
    /// <remarks>
    /// Common metadata keys by binding type:
    /// - HTTP: url, method, content-type
    /// - MQTT: topic, qos
    /// - SignalR: hub, group, user
    /// - Kafka: topic, key, partition
    /// </remarks>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// The classified binding kind (determined at runtime from Dapr metadata).
    /// Used for binding-specific payload formatting.
    /// </summary>
    public string? BindingKind { get; init; }
}

