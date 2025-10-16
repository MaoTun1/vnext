using BBT.Workflow.Domain;

namespace BBT.Workflow.Execution;

/// <summary>
/// Factory for creating TransitionExecutionContext instances.
/// Handles rehydration of workflow definitions, instances, and context setup.
/// </summary>
public interface ITransitionContextFactory
{
    /// <summary>
    /// Creates a new TransitionExecutionContext by rehydrating all necessary data.
    /// </summary>
    /// <param name="context">The workflow execution context containing the request details.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A fully populated TransitionExecutionContext ready for execution.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transition is invalid for the trigger type.</exception>
    Task<Result<TransitionExecutionContext>> CreateAsync(WorkflowExecutionContext context, CancellationToken cancellationToken);
}
