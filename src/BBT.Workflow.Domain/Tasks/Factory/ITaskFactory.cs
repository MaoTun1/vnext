using BBT.Aether.Results;
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
    /// <returns>A Result containing the task instance ready for execution, or an error if creation failed.</returns>
    Task<Result<WorkflowTask>> CreateExecutionTaskAsync(IReference taskReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a task instance from cached data with optimized cloning strategy.
    /// </summary>
    /// <param name="cachedTask">The cached task instance to create from.</param>
    /// <returns>A Result containing the new task instance isolated from the cached one, or an error if cloning failed.</returns>
    Result<WorkflowTask> CreateFromCached(WorkflowTask cachedTask);
}

