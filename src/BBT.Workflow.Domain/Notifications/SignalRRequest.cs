using System.Text.Json;

namespace BBT.Workflow.Notifications;

/// <summary>
/// SignalR request DTO for workflow notifications
/// </summary>
public class SignalRRequest
{
    /// <summary>
    /// Gets or sets the request ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source of the request
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the request
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject of the request
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data payload
    /// </summary>
    public JsonElement? Data { get; set; }
}

