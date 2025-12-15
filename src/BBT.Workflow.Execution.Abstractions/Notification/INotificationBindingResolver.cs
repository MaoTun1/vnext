namespace BBT.Workflow.Execution.Notification;

/// <summary>
/// Resolves the notification binding component from Dapr metadata.
/// Provides lazy-loaded, cached access to the binding configuration.
/// </summary>
public interface INotificationBindingResolver
{
    /// <summary>
    /// Resolves and classifies the notification binding component.
    /// The result is cached after first resolution.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved notification binding information.</returns>
    Task<NotificationBindingInfo> ResolveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the binding has been resolved.
    /// </summary>
    bool IsResolved { get; }
}

