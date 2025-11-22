using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.Factory;

/// <summary>
/// Factory implementation for creating workflow task instances with caching support and performance optimization.
/// This factory ensures task instances are properly isolated from cached data to prevent state pollution.
/// </summary>
public sealed class TaskFactory(
    IComponentCacheStore componentCacheStore,
    ILogger<TaskFactory> logger)
    : ITaskFactory
{
    /// <summary>
    /// Creates a task instance suitable for execution, ensuring it's isolated from cached instances.
    /// </summary>
    /// <param name="taskReference">Reference to the task to be created.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task instance ready for execution, isolated from cache.</returns>
    public async Task<WorkflowTask> CreateExecutionTaskAsync(
        IReference taskReference, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedTask = await componentCacheStore.GetTaskAsync(taskReference, cancellationToken);
            return CreateFromCached(cachedTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create execution task for reference: {TaskReference}", taskReference);
            throw;
        }
    }

    /// <summary>
    /// Creates a task instance from cached data with optimized cloning strategy.
    /// Uses the task's own Clone method for type-specific optimization.
    /// </summary>
    /// <param name="cachedTask">The cached task instance to create from.</param>
    /// <returns>A new task instance isolated from the cached one.</returns>
    public WorkflowTask CreateFromCached(WorkflowTask cachedTask)
    {
        if (cachedTask == null)
            throw new ArgumentNullException(nameof(cachedTask));

        try
        {
            // Use the task's own optimized Clone method
            var clonedTask = cachedTask.Clone();
            return clonedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clone task {TaskKey} of type {TaskType}", 
                cachedTask.Key, cachedTask.GetType().Name);
            throw;
        }
    }
} 