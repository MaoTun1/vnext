using BBT.Aether.Results;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Tasks.Executors;

/// <summary>
/// Registry for resolving task executors based on task type.
/// Provides a centralized way to discover and retrieve executors.
/// </summary>
public interface ITaskExecutorRegistry
{
    /// <summary>
    /// Gets the executor for the specified task type.
    /// </summary>
    /// <param name="taskType">The task type to get an executor for.</param>
    /// <returns>A result containing the executor or an error if not found.</returns>
    Result<ITaskExecutor> GetExecutor(TaskType taskType);

    /// <summary>
    /// Checks if an executor exists for the specified task type.
    /// </summary>
    /// <param name="taskType">The task type to check.</param>
    /// <returns>True if an executor exists, false otherwise.</returns>
    bool HasExecutor(TaskType taskType);
}

