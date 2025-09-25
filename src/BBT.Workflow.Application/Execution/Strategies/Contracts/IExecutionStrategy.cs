namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Defines the contract for workflow execution strategies.
/// Implements the Strategy Pattern to handle different execution modes (sync/async).
/// </summary>
public interface IExecutionStrategy
{
    /// <summary>
    /// Gets a value indicating whether this strategy supports the specified execution mode.
    /// </summary>
    /// <param name="isSync">Whether the execution should be synchronous.</param>
    /// <returns>True if the strategy supports the execution mode; otherwise, false.</returns>
    bool CanHandle(bool isSync);
}
