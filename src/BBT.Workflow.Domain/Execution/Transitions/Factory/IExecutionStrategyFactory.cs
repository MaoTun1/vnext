namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Factory for creating appropriate execution strategies based on execution mode.
/// </summary>
public interface IExecutionStrategyFactory
{
    /// <summary>
    /// Gets the appropriate execution strategy for the specified execution mode.
    /// </summary>
    /// <param name="mode">The execution mode (sync/async).</param>
    /// <returns>The strategy capable of handling the specified execution mode.</returns>
    /// <exception cref="NotSupportedException">Thrown when no strategy is found for the execution mode.</exception>
    ITransitionStrategy Get(ExecMode mode);
}
