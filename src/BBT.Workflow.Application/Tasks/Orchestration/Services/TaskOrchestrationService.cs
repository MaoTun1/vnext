using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Tasks;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using BBT.Workflow.Instances;
using BBT.Workflow.Tasks.Factory;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Service responsible for orchestrating workflow tasks with support for both parallel and sequential execution strategies.
/// </summary>
/// <remarks>
/// The TaskOrchestrationService coordinates task execution based on dependencies and provides 
/// both condition evaluation and task orchestration capabilities. It manages task state transitions
/// and maintains execution context throughout the workflow process.
/// </remarks>
public class TaskOrchestrationService(
    ITaskOrchestrator taskOrchestrator,
    ITaskExecutorFactory taskExecutorFactory,
    ITaskFactory taskFactory) : ITaskOrchestrationService
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
    public async Task ExecuteAsync(
        IEnumerable<OnExecuteTask> onExecuteTasks,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var tasks = onExecuteTasks.ToList();
        if (!tasks.Any()) return;

        // Check if tasks can be executed in parallel (no dependencies)
        var canExecuteInParallel = CanExecuteInParallel(tasks);

        if (canExecuteInParallel)
        {
            await ExecuteTasksInParallelAsync(tasks, instanceTransition, taskTrigger, context, cancellationToken);
        }
        else
        {
            await ExecuteTasksSequentiallyAsync(tasks, instanceTransition, taskTrigger, context, cancellationToken);
        }
    }

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
    public async Task<bool> ExecuteConditionAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var task = ConditionTask.Create();
        var taskExecutor = taskExecutorFactory.GetExecutor(task.GetTaskType());

        var response = await taskExecutor.ExecuteAsync(task, script.DecodedCode, context, cancellationToken);
        return response as bool? ?? false;
    }

    /// <summary>
    /// Orchestrates multiple tasks concurrently in parallel to improve performance.
    /// </summary>
    /// <param name="onExecuteTasks">List of tasks to orchestrate in parallel.</param>
    /// <param name="instanceTransition">The instance transition context. Can be null for extension tasks.</param>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <param name="context">The script execution context shared across all parallel tasks.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation when all parallel tasks complete.</returns>
    /// <remarks>
    /// This method uses Task.WhenAll to orchestrate tasks concurrently. Thread-safe operations
    /// are used for context updates to prevent race conditions.
    /// </remarks>
    private async Task ExecuteTasksInParallelAsync(
        IList<OnExecuteTask> onExecuteTasks,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        var executionTasks = onExecuteTasks.Select(async onExecuteTask =>
        {
            await taskOrchestrator.ExecuteTaskAsync(onExecuteTask, instanceTransition, taskTrigger, context, cancellationToken);
        });

        await Task.WhenAll(executionTasks);
    }

    /// <summary>
    /// Orchestrates tasks one after another in sequential order to maintain dependencies.
    /// </summary>
    /// <param name="onExecuteTasks">List of tasks to orchestrate sequentially.</param>
    /// <param name="instanceTransition">The instance transition context. Can be null for extension tasks.</param>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <param name="context">The script execution context that accumulates results from each task.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation when all sequential tasks complete.</returns>
    /// <remarks>
    /// Sequential orchestration is used when tasks have dependencies or when parallel execution
    /// is not suitable based on the task order analysis.
    /// </remarks>
    private async Task ExecuteTasksSequentiallyAsync(
        IList<OnExecuteTask> onExecuteTasks,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        foreach (var onExecuteTask in onExecuteTasks)
        {
            await taskOrchestrator.ExecuteTaskAsync(onExecuteTask, instanceTransition, taskTrigger, context, cancellationToken);
        }
    }

    /// <summary>
    /// Determines whether the given tasks can be executed in parallel based on their configuration.
    /// </summary>
    /// <param name="tasks">List of tasks to analyze for parallel execution capability.</param>
    /// <returns>
    /// True if tasks can be executed in parallel (no dependencies), false if sequential execution is required.
    /// </returns>
    /// <remarks>
    /// Currently uses a simple heuristic based on task order values. Tasks with the same order
    /// or single tasks are considered safe for parallel execution. In future implementations,
    /// this could be enhanced to analyze actual task dependencies.
    /// </remarks>
    private static bool CanExecuteInParallel(IList<OnExecuteTask> tasks)
    {
        // Simple heuristic: if tasks have different orders, they might have dependencies
        // In a more sophisticated implementation, you would analyze actual dependencies
        var orders = tasks.Select(t => t.Order).Distinct().ToList();
        return orders.Count == 1 || tasks.Count == 1;
    }
} 