using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Defines the contract for workflow instance start execution strategies.
/// </summary>
public interface IInstanceStartStrategy : IExecutionStrategy
{
    /// <summary>
    /// Executes the instance start operation according to the strategy's execution mode.
    /// </summary>
    /// <param name="context">The execution context containing all necessary data for starting an instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The start instance response.</returns>
    Task<InstanceServiceResponse<StartInstanceOutput>> ExecuteAsync(
        InstanceStartExecutionContext context,
        CancellationToken cancellationToken = default);
}
