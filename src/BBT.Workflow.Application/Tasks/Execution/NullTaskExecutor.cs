using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.Execution;

/// <summary>
/// Null Object Pattern implementation of ITaskOrchestrator that performs no operations.
/// This is the default implementation that does nothing, serving as a safe no-op fallback.
/// It should be replaced by specific implementations (DaprTaskExecutor for Orchestration, 
/// LocalTaskExecutor for Execution) in respective service hosts.
/// </summary>
/// <remarks>
/// This implementation follows the Null Object Pattern, providing a "do nothing" behavior
/// that eliminates the need for null checks and provides a safe default when no specific
/// task orchestrator is configured. This is particularly useful in scenarios where task
/// execution is not required or is handled by a different mechanism.
/// 
/// Key Design Decisions:
/// - Logs a warning to make it clear when the null implementation is being used
/// - Does not throw exceptions, following the Null Object Pattern principle
/// - Returns immediately without any processing
/// - Thread-safe and stateless
/// </remarks>
/// <param name="logger">Logger for recording when null executor is invoked.</param>
public sealed class NullTaskExecutor(ILogger<NullTaskExecutor> logger) : ITaskOrchestrator
{
    /// <summary>
    /// Performs no operation for task execution. This is the null implementation that logs 
    /// a warning and returns immediately without processing.
    /// </summary>
    /// <param name="onExecuteTask">The task configuration (ignored).</param>
    /// <param name="instanceTransitionId">The instance transition context (ignored).</param>
    /// <param name="taskTrigger">The trigger type (ignored).</param>
    /// <param name="context">The script execution context (ignored).</param>
    /// <param name="cancellationToken">Cancellation token (ignored).</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// This method intentionally does nothing. It exists to satisfy the interface contract
    /// and provide a safe no-op behavior. If this method is being called, it typically indicates
    /// that the proper ITaskOrchestrator implementation (DaprTaskExecutor or LocalTaskExecutor)
    /// has not been registered in the DI container for the current service.
    /// </remarks>
    public Task ExecuteTaskAsync(
        OnExecuteTask onExecuteTask,
        Guid? instanceTransitionId,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "NullTaskExecutor is being used for task {TaskKey} on instance {InstanceId}. " +
            "This is a no-op implementation. Ensure the proper ITaskOrchestrator " +
            "(DaprTaskExecutor for Orchestration or LocalTaskExecutor for Execution) " +
            "is registered in your service's DI container.",
            onExecuteTask?.Task?.Key ?? "Unknown",
            context?.Instance?.Id ?? Guid.Empty);
        
        return Task.CompletedTask;
    }
}

