using BBT.Workflow.Definitions;

namespace BBT.Workflow.Execution.TriggerTransition;

/// <summary>
/// Factory for creating appropriate trigger transition strategies based on trigger type.
/// </summary>
public interface ITriggerTransitionStrategyFactory
{
    /// <summary>
    /// Gets the appropriate trigger transition strategy for the specified trigger type.
    /// </summary>
    /// <param name="type">The trigger transition type (Start, Trigger, SubProcess).</param>
    /// <returns>The strategy capable of handling the specified trigger type.</returns>
    /// <exception cref="NotSupportedException">Thrown when no strategy is found for the trigger type.</exception>
    ITriggerTransitionStrategy Get(TriggerTransitionType type);
}

