using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.Executors;

/// <summary>
/// Defines the contract for task executors that handle specific task types.
/// Each executor manages the complete lifecycle of a task including:
/// - Input mapping and preparation
/// - Pre-processing
/// - Invocation (local or remote)
/// - Post-processing (e.g., correlation saving)
/// - Output mapping
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// The task type this executor handles.
    /// </summary>
    TaskType TaskType { get; }

    /// <summary>
    /// Executes the task with the given context.
    /// </summary>
    /// <param name="context">The execution context containing task, mapping, and script context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the standard task response.</returns>
    Task<Result<StandardTaskResponse>> ExecuteAsync(
        TaskExecutorContext context,
        CancellationToken cancellationToken = default);
}

