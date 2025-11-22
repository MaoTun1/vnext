using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Defines the contract for transition execution strategies.
/// Implements the Strategy Pattern to handle different execution modes (sync/async).
/// </summary>
public interface ITransitionStrategy
{
    /// <summary>
    /// Executes the provided action using the strategy's execution mode.
    /// </summary>
    /// <param name="context">The workflow execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<Result<TransitionExecutionContext>> ExecuteAsync(WorkflowExecutionContext context, CancellationToken cancellationToken);
}