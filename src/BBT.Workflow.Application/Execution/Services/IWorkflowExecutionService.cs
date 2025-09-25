using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Service interface for orchestrating workflow execution operations.
/// Implements Command Pattern to encapsulate execution requests.
/// </summary>
public interface IWorkflowExecutionService
{
    /// <summary>
    /// Executes a workflow instance start operation using the appropriate execution strategy.
    /// </summary>
    /// <param name="input">The start instance input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The start instance response.</returns>
    Task<InstanceServiceResponse<StartInstanceOutput>> ExecuteStartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a workflow transition operation using the appropriate execution strategy.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="transitionKey">The transition key.</param>
    /// <param name="input">The transition input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transition response.</returns>
    Task<InstanceServiceResponse<TransitionOutput>> ExecuteTransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
}
