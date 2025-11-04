using System.Text.Json.Serialization;

namespace BBT.Workflow.Notifications;

/// <summary>
/// Represents a notification message to be sent through notification channels (SignalR, MQTT, etc.)
/// </summary>
public class NotificationMessage
{
    /// <summary>
    /// Gets or sets the unique identifier of the message
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }


    /// <summary>
    /// Gets or sets the source of the message
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    /// <summary>
    /// Gets or sets the type of the message
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the subject of the message
    /// </summary>
    [JsonPropertyName("subject")]
    public required string Subject { get; set; }

    /// <summary>
    /// Gets or sets the data payload of the message
    /// </summary>
    [JsonPropertyName("data")]
    public required object Data { get; set; }

    /// <summary>
    /// Creates a new notification message with the specified identifier and data.
    /// The source, type, and subject are set to default values: "vnext", "vnext.workflow", and "workflow-completed" respectively.
    /// </summary>
    /// <param name="id">The unique identifier of the message.</param>
    /// <param name="data">The data payload of the message.</param>
    /// <returns>A new instance of NotificationMessage.</returns>
    public static NotificationMessage Create(string id, object data)
    {
        return new NotificationMessage
        {
            Id = id,
            Source = "vnext",
            Type = "vnext.workflow",
            Subject = "workflow-completed",
            Data = data
        };
    }
}

