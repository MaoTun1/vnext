using BBT.Workflow.Definitions;

namespace BBT.Workflow.Tasks.Factory;

/// <summary>
/// Defines a factory contract for creating workflow task instances.
/// This factory abstracts task creation logic and provides caching-aware task instantiation.
/// </summary>
public interface ITaskFactory
{
    /// <summary>
    /// Creates a task instance suitable for execution, ensuring it's isolated from cached instances.
    /// </summary>
    /// <param name="taskReference">Reference to the task to be created.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task instance ready for execution, isolated from cache.</returns>
    Task<WorkflowTask> CreateExecutionTaskAsync(IReference taskReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a task instance from cached data with optimized cloning strategy.
    /// </summary>
    /// <param name="cachedTask">The cached task instance to create from.</param>
    /// <returns>A new task instance isolated from the cached one.</returns>
    WorkflowTask CreateFromCached(WorkflowTask cachedTask);
} 