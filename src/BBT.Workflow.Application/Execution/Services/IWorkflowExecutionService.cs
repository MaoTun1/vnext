using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Service interface for orchestrating workflow execution operations using the new pipeline architecture.
/// </summary>
public interface IWorkflowExecutionService
{
    /// <summary>
    /// Executes a workflow transition using the new pipeline architecture.
    /// </summary>
    /// <param name="context">The workflow execution input containing all necessary data.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A service response containing the transition result.</returns>
    Task<InstanceServiceResponse<TransitionOutput>> ExecuteTransitionAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}