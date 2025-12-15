using BBT.Workflow.Execution.Invokers;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Registry for task invokers.
/// Routes task invocations to the appropriate invoker based on task type.
/// </summary>
public interface ITaskInvokerRegistry
{
    /// <summary>
    /// Gets an invoker for the specified task type.
    /// </summary>
    /// <param name="taskType">The task type (e.g., "http", "daprService").</param>
    /// <returns>The invoker or null if not found.</returns>
    ITaskInvoker? GetInvoker(string taskType);
    
    /// <summary>
    /// Checks if an invoker is registered for the specified task type.
    /// </summary>
    /// <param name="taskType">The task type to check.</param>
    /// <returns>True if an invoker is registered.</returns>
    bool HasInvoker(string taskType);
    
    /// <summary>
    /// Invokes a task using the envelope-based routing.
    /// </summary>
    /// <param name="envelope">The task envelope containing type and binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task invocation result.</returns>
    Task<TaskInvocationResult> InvokeAsync(
        TaskEnvelope envelope,
        CancellationToken cancellationToken = default);
}

