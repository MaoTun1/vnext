using BBT.Aether.Results;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Orchestrates transition chaining with isolated DI scope and UoW per hop.
/// Owns the execution loop: each transition runs in its own scope with RequiresNew UoW.
/// </summary>
public interface ITransitionRunner
{
    /// <summary>
    /// Runs a transition and any subsequent inline auto chain transitions.
    /// Each hop is executed in a new DI scope with RequiresNew UoW for complete isolation.
    /// </summary>
    /// <param name="context">The initial workflow execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A Result containing the final transition output from the chain.</returns>
    Task<Result<TransitionOutput>> RunAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}

