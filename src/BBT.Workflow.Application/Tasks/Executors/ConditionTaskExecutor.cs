using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes condition-based workflow tasks that evaluate boolean expressions or conditional logic.
/// This executor is responsible for running script code that determines workflow branching decisions.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling and executing condition scripts.</param>
/// <param name="logger">The logger instance for logging condition task execution details.</param>
public sealed class ConditionTaskExecutor(
    IScriptEngine scriptEngine,
    ILogger<ConditionTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a condition task by compiling the script code into a condition mapping and evaluating it.
    /// </summary>
    /// <param name="task">The workflow task containing the condition logic to evaluate.</param>
    /// <param name="scriptCode">The script code that implements the condition evaluation logic.</param>
    /// <param name="context">The script context containing data and variables for condition evaluation.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the boolean result
    /// or conditional data from the condition evaluation.
    /// </returns>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scriptRunner = await ScriptEngine.CompileToInstanceAsync<IConditionMapping>(
                scriptCode,
                cancellationToken: cancellationToken);
            
            var response = await scriptRunner.Handler(context);
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during condition task {TaskKey} execution after,", 
                task.Key);
            throw;
        }
    }
}