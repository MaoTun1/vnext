using BBT.Workflow.Definitions;

namespace BBT.Workflow.Tasks.Persistence;

/// <summary>
/// Factory implementation that creates and returns appropriate task persistence strategies
/// based on the TaskTrigger type using dependency injection.
/// </summary>
/// <remarks>
/// This factory uses a collection of registered strategies and selects the first one
/// that can handle the given TaskTrigger. The factory pattern provides flexibility
/// for adding new strategies without modifying existing code.
/// </remarks>
public sealed class TaskPersistenceStrategyFactory(
    IEnumerable<ITaskPersistenceStrategy> strategies) : ITaskPersistenceStrategyFactory
{
    private readonly IReadOnlyList<ITaskPersistenceStrategy> _strategies = strategies.ToList();

    /// <summary>
    /// Gets the appropriate task persistence strategy for the given TaskTrigger.
    /// </summary>
    /// <param name="taskTrigger">The trigger type that determines which strategy to use.</param>
    /// <returns>The strategy instance that can handle the specified TaskTrigger.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no strategy can handle the given TaskTrigger.</exception>
    public ITaskPersistenceStrategy GetStrategy(TaskTrigger taskTrigger)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(taskTrigger));
        
        if (strategy == null)
        {
            throw new InvalidOperationException(
                $"No task persistence strategy found for TaskTrigger: {taskTrigger}");
        }

        return strategy;
    }
} 