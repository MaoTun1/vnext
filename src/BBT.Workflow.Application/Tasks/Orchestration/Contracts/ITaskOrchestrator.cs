using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Abstraction for orchestrating individual workflow tasks.
/// This interface provides a clean separation between task orchestration and specific task execution.
/// </summary>
/// <remarks>
/// Implementations can be local (direct orchestration) or remote (via Dapr Service Invocation).
/// This follows the Single Responsibility Principle by separating task orchestration from concrete task execution.
/// </remarks>
public interface ITaskOrchestrator
{
    /// <summary>
    /// Orchestrates the execution of a workflow task.
    /// </summary>
    /// <param name="onExecuteTask">The task configuration to orchestrate.</param>
    /// <param name="instanceTransition">The instance transition context. Can be null for extension tasks.</param>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <param name="context">The script execution context for task execution.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    Task ExecuteTaskAsync(
        OnExecuteTask onExecuteTask,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default);
} 