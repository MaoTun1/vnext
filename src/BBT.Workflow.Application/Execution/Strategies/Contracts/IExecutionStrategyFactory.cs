namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Factory interface for creating execution strategies based on execution mode.
/// </summary>
public interface IExecutionStrategyFactory
{
    /// <summary>
    /// Gets the appropriate instance start strategy for the specified execution mode.
    /// </summary>
    /// <param name="isSync">Whether the execution should be synchronous.</param>
    /// <returns>The instance start strategy.</returns>
    /// <exception cref="NotSupportedException">Thrown when no strategy is found for the execution mode.</exception>
    IInstanceStartStrategy GetInstanceStartStrategy(bool isSync);

    /// <summary>
    /// Gets the appropriate transition strategy for the specified execution mode.
    /// </summary>
    /// <param name="isSync">Whether the execution should be synchronous.</param>
    /// <returns>The transition strategy.</returns>
    /// <exception cref="NotSupportedException">Thrown when no strategy is found for the execution mode.</exception>
    ITransitionStrategy GetTransitionStrategy(bool isSync);
}
