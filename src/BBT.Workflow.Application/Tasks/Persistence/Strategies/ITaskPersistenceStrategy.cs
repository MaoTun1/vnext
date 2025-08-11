using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Tasks.Persistence;

/// <summary>
/// Strategy interface for handling task persistence based on different trigger types.
/// This interface enables different persistence behaviors for various TaskTrigger scenarios
/// while maintaining separation of concerns and following SOLID principles.
/// </summary>
public interface ITaskPersistenceStrategy
{
    /// <summary>
    /// Determines if this strategy should handle the given task execution context.
    /// </summary>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <returns>True if this strategy should handle the task, false otherwise.</returns>
    bool CanHandle(TaskTrigger taskTrigger);

    /// <summary>
    /// Handles the creation and initial persistence of an InstanceTask if required.
    /// </summary>
    /// <param name="instanceTask">The InstanceTask to be persisted.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleCreationAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the completion and final persistence of an InstanceTask if required.
    /// </summary>
    /// <param name="instanceTask">The InstanceTask to be updated.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleCompletionAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default);
} 