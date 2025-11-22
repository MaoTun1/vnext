using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes general script-based workflow tasks that perform data transformation and business logic.
/// This executor compiles and runs custom script code to process workflow data and return results.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling and executing workflow scripts.</param>
/// <param name="logger">The logger instance for logging script task execution details.</param>
public sealed class ScriptTaskExecutor(
    IScriptEngine scriptEngine,
    ILogger<ScriptTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a script task by compiling the script code into a mapping instance and processing the output.
    /// </summary>
    /// <param name="task">The workflow task containing the script execution configuration.</param>
    /// <param name="scriptCode">The script code that implements the data transformation and business logic.</param>
    /// <param name="context">The script context containing input data and variables for script execution.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the transformed data
    /// from the script execution output handler.
    /// </returns>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var scriptRunner = await ScriptEngine.CompileToInstanceAsync<IMapping>(
                scriptCode, 
                cancellationToken: cancellationToken);
            
            await scriptRunner.InputHandler((task as ScriptTask)!, context);
            
            var upResponse = await scriptRunner.OutputHandler(context);
            
            stopwatch.Stop();
            
            return upResponse;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error occurred during script task {TaskKey} execution after {Duration}ms", 
                task.Key, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
} 