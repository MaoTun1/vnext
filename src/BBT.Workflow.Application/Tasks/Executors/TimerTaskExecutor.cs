using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

public class TimerTaskExecutor(
    IScriptEngine scriptEngine,
    ILogger<ConditionTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Logger.LogInformation("Starting timer task execution for task {TaskKey}", task.Key);
        
        try
        {
            Logger.LogDebug("Compiling timer script code for task {TaskKey}", task.Key);
            var scriptRunner = await ScriptEngine.CompileToInstanceAsync<ITimerMapping>(
                scriptCode,
                cancellationToken: cancellationToken);

            Logger.LogDebug("Condition script compiled successfully, executing condition handler for task {TaskKey}", task.Key);
            var response = await scriptRunner.Handler(context);
            
            stopwatch.Stop();
            Logger.LogInformation("Timer task {TaskKey} completed successfully in {Duration}ms with result: {Result}", 
                task.Key, stopwatch.ElapsedMilliseconds, response);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error occurred during timer task {TaskKey} execution after {Duration}ms", 
                task.Key, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}