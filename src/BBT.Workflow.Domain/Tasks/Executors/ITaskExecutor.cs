using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Defines a contract for executing workflow tasks with script code and context.
/// Implementations handle different types of tasks including HTTP calls, DAPR operations, 
/// script execution, and other workflow activities.
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Executes a workflow task asynchronously using the provided script code and context.
    /// </summary>
    /// <param name="task">The workflow task to execute. The actual task type determines the execution behavior.</param>
    /// <param name="scriptCode">The script code to compile and execute as part of the task processing.</param>
    /// <param name="context">The script context containing variables, data, and execution state for the workflow.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during the execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the output data 
    /// from the task execution, or null if no output is produced.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the task type is not supported by this executor.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the script compilation or execution fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken);
}