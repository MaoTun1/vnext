using BBT.Workflow.Definitions;

namespace BBT.Workflow.Tasks.Persistence;

/// <summary>
/// Factory interface for creating appropriate task persistence strategies based on TaskTrigger types.
/// This factory encapsulates the strategy selection logic and provides a clean abstraction
/// for obtaining the correct persistence behavior.
/// </summary>
public interface ITaskPersistenceStrategyFactory
{
    /// <summary>
    /// Gets the appropriate task persistence strategy for the given TaskTrigger.
    /// </summary>
    /// <param name="taskTrigger">The trigger type that determines which strategy to use.</param>
    /// <returns>The strategy instance that can handle the specified TaskTrigger.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no strategy can handle the given TaskTrigger.</exception>
    ITaskPersistenceStrategy GetStrategy(TaskTrigger taskTrigger);
} 