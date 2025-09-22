namespace BBT.Workflow.SubFlow;

/// <summary>
/// Service for handling SubFlow completion events and processing parent workflow continuation.
/// </summary>
public interface ISubFlowCompletionService
{
    /// <summary>
    /// Handles SubFlow completion by processing output mapping and resuming parent workflow execution.
    /// </summary>
    /// <param name="completedData">The completed SubFlow data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the completion handling operation</returns>
    Task HandleSubFlowCompletionAsync(FlowCompletedData completedData, CancellationToken cancellationToken = default);
}
