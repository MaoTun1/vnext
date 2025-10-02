using BBT.Workflow.Instances;

namespace BBT.Workflow.Notifications;

/// <summary>
/// Service interface for sending SignalR notifications
/// </summary>
public interface ISignalRNotificationService
{
    /// <summary>
    /// Sends a workflow completion notification via SignalR
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <param name="domain">The workflow domain</param>
    /// <param name="workflow">The workflow key</param>
    /// <param name="headers">Request headers to include in the HTTP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task SendWorkflowCompletedNotificationAsync(
        Guid instanceId,
        string domain,
        string workflow,
        Dictionary<string, string?>? headers,
        CancellationToken cancellationToken = default);
}
