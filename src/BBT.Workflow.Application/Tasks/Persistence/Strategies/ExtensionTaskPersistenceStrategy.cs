using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Tasks.Persistence.Strategies;

/// <summary>
/// Extension implementation of task persistence strategy that skips database persistence
/// for Extension task execution scenarios.
/// </summary>
/// <remarks>
/// This strategy is used for extension tasks that should not be persisted to the database.
/// Extension tasks are typically used for data enrichment purposes and don't need to be
/// part of the permanent workflow execution history. Uses the Result pattern for consistency.
/// </remarks>
public sealed class ExtensionTaskPersistenceStrategy : ITaskPersistenceStrategy
{
    /// <summary>
    /// Determines if this strategy should handle the task persistence.
    /// Returns true only for Extension TaskTrigger type.
    /// </summary>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <returns>True if the task trigger is Extension, false otherwise.</returns>
    public bool CanHandle(TaskTrigger taskTrigger)
    {
        return taskTrigger == TaskTrigger.Extension;
    }

    /// <summary>
    /// Handles the creation phase for Extension tasks.
    /// No database operation is performed as Extension tasks should not be persisted.
    /// </summary>
    /// <param name="instanceTask">The InstanceTask (not persisted for Extension tasks).</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A successful Result indicating no-op completion.</returns>
    public Task<Result> HandleCreationAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default)
    {
        // Extension tasks are not persisted to database
        return Task.FromResult(Result.Ok());
    }

    /// <summary>
    /// Handles the completion phase for Extension tasks.
    /// No database operation is performed as Extension tasks should not be persisted.
    /// </summary>
    /// <param name="instanceTask">The InstanceTask (not persisted for Extension tasks).</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A successful Result indicating no-op completion.</returns>
    public Task<Result> HandleCompletionAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default)
    {
        // Extension tasks are not persisted to database
        return Task.FromResult(Result.Ok());
    }
} 