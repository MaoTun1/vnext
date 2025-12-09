namespace BBT.Workflow.Execution.Notification;

/// <summary>
/// Represents the type of notification binding component.
/// Determined by parsing the Dapr component type string.
/// </summary>
public enum NotificationBindingKind
{
    /// <summary>
    /// Unknown or unsupported binding type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// HTTP binding (bindings.http).
    /// </summary>
    Http = 1,

    /// <summary>
    /// MQTT binding (bindings.mqtt).
    /// </summary>
    Mqtt = 2,

    /// <summary>
    /// SignalR binding (bindings.signalr).
    /// </summary>
    SignalR = 3,

    /// <summary>
    /// Kafka binding (bindings.kafka).
    /// </summary>
    Kafka = 4,

    /// <summary>
    /// Redis binding (bindings.redis).
    /// </summary>
    Redis = 5,

    /// <summary>
    /// RabbitMQ binding (bindings.rabbitmq).
    /// </summary>
    RabbitMq = 6
}

