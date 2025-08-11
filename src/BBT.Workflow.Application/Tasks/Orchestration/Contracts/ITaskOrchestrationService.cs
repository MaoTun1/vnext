using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Tasks;
using BBT.Workflow.Scripting;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Interface for service responsible for orchestrating workflow tasks with support for both parallel and sequential execution strategies.
/// </summary>
/// <remarks>
/// The ITaskOrchestrationService interface defines the contract for coordinating task execution based on dependencies and provides 
/// both condition evaluation and task orchestration capabilities. It manages task state transitions
/// and maintains execution context throughout the workflow process.
/// </remarks>
public interface ITaskOrchestrationService
{
    /// <summary>
    /// Orchestrates a collection of tasks using the optimal execution strategy (parallel or sequential).
    /// </summary>
    /// <param name="onExecuteTasks">Collection of tasks to be orchestrated.</param>
    /// <param name="instanceTransition">The instance transition context. Can be null for extension tasks.</param>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <param name="context">The script execution context containing instance data and task responses.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    Task ExecuteAsync(
        IEnumerable<OnExecuteTask> onExecuteTasks,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a condition script and returns the boolean result.
    /// </summary>
    /// <param name="script">The script code containing the condition logic to evaluate.</param>
    /// <param name="context">The script execution context for condition evaluation.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains 
    /// true if the condition is met, false otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when script or context is null.</exception>
    Task<bool> ExecuteConditionAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
} 