using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes human workflow tasks that require manual intervention or approval through DAPR runtime.
/// This executor is designed to handle workflow steps that require human interaction or decision-making.
/// Currently, this implementation is a placeholder and not yet implemented.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="logger">The logger instance for logging human task execution details.</param>
public sealed class DaprHumanTaskExecutor(
    IScriptEngine scriptEngine,
    ILogger<DaprHumanTaskExecutor> logger) 
    : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a human workflow task that requires manual intervention or approval.
    /// </summary>
    /// <param name="task">The human workflow task containing the configuration for manual processing.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing for the human task.</param>
    /// <param name="context">The script context containing data for task preparation and processing.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed data
    /// from the human task execution.
    /// </returns>
    /// <exception cref="NotImplementedException">Currently thrown as this executor is not yet implemented.</exception>
    public Task<object?> ExecuteAsync(
        WorkflowTask task, 
        string scriptCode, 
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("DaprHumanTaskExecutor is not yet implemented");
    }
}