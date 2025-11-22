namespace BBT.Workflow.Workers.Outbox.Services;

/// <summary>
/// Interface for processing outbox messages across multiple database schemas.
/// </summary>
public interface IMultiSchemaOutboxProcessor
{
    /// <summary>
    /// Processes outbox messages for all schemas in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RunAsync(CancellationToken cancellationToken = default);
}

