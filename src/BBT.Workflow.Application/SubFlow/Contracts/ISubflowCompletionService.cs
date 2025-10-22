namespace BBT.Workflow.SubFlow;

/// <summary>
/// Service for handling SubFlow completion events and processing parent workflow continuation.
/// </summary>
public interface ISubflowCompletionService
{
    /// <summary>
    /// Handles SubFlow completion by processing output mapping and resuming parent workflow execution.
    /// </summary>
    /// <param name="completedDataEto">The completed SubFlow data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the completion handling operation</returns>
    Task HandleSubFlowCompletionAsync(FlowCompletedDataEto completedDataEto, CancellationToken cancellationToken = default);
}
