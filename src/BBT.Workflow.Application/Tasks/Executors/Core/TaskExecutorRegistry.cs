using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.Executors;

/// <summary>
/// Registry for resolving task executors based on task type.
/// Uses DI-injected IEnumerable to discover all registered executors.
/// Scoped lifecycle compatible - no internal caching.
/// </summary>
public sealed class TaskExecutorRegistry : ITaskExecutorRegistry
{
    private readonly IEnumerable<ITaskExecutor> _executors;
    private readonly ILogger<TaskExecutorRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of TaskExecutorRegistry.
    /// </summary>
    public TaskExecutorRegistry(
        IEnumerable<ITaskExecutor> executors,
        ILogger<TaskExecutorRegistry> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    /// <inheritdoc />
    public Result<ITaskExecutor> GetExecutor(TaskType taskType)
    {
        var executor = _executors.FirstOrDefault(e => e.TaskType == taskType);
        
        if (executor == null)
        {
            _logger.LogWarning("No executor found for task type {TaskType}", taskType);
            return Result<ITaskExecutor>.Fail(Error.NotFound(
                WorkflowErrorCodes.TaskExecution,
                $"No executor registered for task type: {taskType}"));
        }

        _logger.LogDebug("Resolved executor {ExecutorType} for task type {TaskType}",
            executor.GetType().Name, taskType);

        return Result<ITaskExecutor>.Ok(executor);
    }

    /// <inheritdoc />
    public bool HasExecutor(TaskType taskType)
    {
        return _executors.Any(e => e.TaskType == taskType);
    }
}

