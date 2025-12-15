namespace BBT.Workflow.Infrastructure.Dapr;

/// <summary>
/// Configuration options for Dapr notification integration.
/// </summary>
public sealed class DaprNotificationOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Dapr:Notification";
    
    /// <summary>
    /// Gets or sets the Dapr binding component name for notifications.
    /// This component will be resolved from Dapr metadata to determine the notification binding type.
    /// </summary>
    /// <example>vnext-notification-binding</example>
    public string ComponentName { get; set; } = "vnext-notification-binding";

    /// <summary>
    /// Gets or sets the default operation for binding invocation.
    /// </summary>
    public string DefaultOperation { get; set; } = "create";

    /// <summary>
    /// Gets or sets the timeout in seconds for Dapr HTTP requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

