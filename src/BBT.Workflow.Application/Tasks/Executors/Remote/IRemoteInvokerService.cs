using BBT.Aether.Results;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.Executors;

/// <summary>
/// Service for invoking tasks remotely via Dapr service invocation.
/// Handles the Dapr communication layer for executors that need remote invocation.
/// </summary>
public interface IRemoteInvokerService
{
    /// <summary>
    /// Invokes a task remotely in the Execution Service.
    /// </summary>
    /// <param name="taskType">The task type identifier.</param>
    /// <param name="taskKey">The task key for logging and tracing.</param>
    /// <param name="envelope">The task envelope containing binding configuration.</param>
    /// <param name="traceContext">The trace context from the script context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the task invocation result.</returns>
    Task<Result<TaskInvocationResult>> InvokeAsync(
        string taskType,
        string taskKey,
        TaskEnvelope envelope,
        TaskTraceContext traceContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a trace context from the script context.
    /// </summary>
    /// <param name="scriptContext">The script context with workflow state.</param>
    /// <returns>A trace context for distributed tracing.</returns>
    TaskTraceContext CreateTraceContext(ScriptContext scriptContext);
}

