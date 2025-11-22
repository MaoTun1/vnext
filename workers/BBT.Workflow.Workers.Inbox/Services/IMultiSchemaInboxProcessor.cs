namespace BBT.Workflow.Workers.Inbox.Services;

/// <summary>
/// Interface for processing inbox messages across multiple database schemas.
/// </summary>
public interface IMultiSchemaInboxProcessor
{
    /// <summary>
    /// Processes inbox messages for all schemas in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RunAsync(CancellationToken cancellationToken = default);
}

