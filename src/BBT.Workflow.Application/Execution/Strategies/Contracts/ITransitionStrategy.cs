using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Defines the contract for workflow transition execution strategies.
/// </summary>
public interface ITransitionStrategy : IExecutionStrategy
{
    /// <summary>
    /// Executes the transition operation according to the strategy's execution mode.
    /// </summary>
    /// <param name="context">The execution context containing all necessary data for transition execution.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transition response.</returns>
    Task<InstanceServiceResponse<TransitionOutput>> ExecuteAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default);
}
