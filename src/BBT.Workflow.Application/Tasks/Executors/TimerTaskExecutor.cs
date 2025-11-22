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
        
        try
        {
            var scriptRunner = await ScriptEngine.CompileToInstanceAsync<ITimerMapping>(
                scriptCode,
                cancellationToken: cancellationToken);
            
            var response = await scriptRunner.Handler(context);
            
            stopwatch.Stop();
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