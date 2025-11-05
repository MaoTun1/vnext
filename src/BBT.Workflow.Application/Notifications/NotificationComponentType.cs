namespace BBT.Workflow.Application.Notifications;

/// <summary>
/// Notification component types that determine how notifications are sent through Dapr components.
/// </summary>
public enum NotificationComponentType
{
    /// <summary>
    /// HTTP binding component for sending notifications via HTTP
    /// </summary>
    HttpBinding = 1,

    /// <summary>
    /// Pub/Sub component (e.g., Kafka) for sending notifications via pub/sub messaging
    /// </summary>
    PubSub = 2,

    /// <summary>
    /// MQTT binding component for sending notifications via MQTT protocol
    /// </summary>
    MqttBinding = 3
}

