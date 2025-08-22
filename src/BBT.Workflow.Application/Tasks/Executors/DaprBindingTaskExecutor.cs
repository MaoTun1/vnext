using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Scripting;
using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes DAPR binding workflow tasks that interact with external systems through DAPR runtime bindings.
/// This executor facilitates integration with external systems like databases, message queues, or cloud services using DAPR's binding building block.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="daprClient">The DAPR client for invoking external system bindings.</param>
/// <param name="workflowMetrics">The workflow metrics service for recording DAPR metrics.</param>
/// <param name="logger">The logger instance for logging DAPR binding task execution details.</param>
public sealed class DaprBindingTaskExecutor(
    IScriptEngine scriptEngine,
    DaprClient daprClient,
    IWorkflowMetrics workflowMetrics,
    ILogger<DaprBindingTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a DAPR binding task by preparing the request through script mapping, invoking the external binding,
    /// and processing any output through output mapping.
    /// </summary>
    /// <param name="task">The DAPR binding workflow task containing binding name, operation, and metadata configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing for the binding operation.</param>
    /// <param name="context">The script context containing data for request preparation and processing.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed data
    /// from the output mapping after binding operation completion.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to DaprBindingTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var daprTask = (task as DaprBindingTask)!;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Logger.LogInformation("Starting DAPR binding task execution for task {TaskKey} - BindingName: {BindingName}, Operation: {Operation}", 
            daprTask.Key, daprTask.BindingName, daprTask.Operation);

        try
        {
            Logger.LogDebug("Preparing input for DAPR binding task {TaskKey}", daprTask.Key);
            var inputResponse = await PrepareInputAsync(daprTask, scriptCode, context, cancellationToken);

            var metadata = daprTask.Metadata.ToDictionary();
            Logger.LogDebug("Prepared metadata for DAPR binding task {TaskKey}: {MetadataCount} entries", 
                daprTask.Key, metadata.Count);

            Logger.LogInformation("Invoking DAPR binding for task {TaskKey}: {BindingName} with operation {Operation}", 
                daprTask.Key, daprTask.BindingName, daprTask.Operation);

            await daprClient.InvokeBindingAsync(
                daprTask.BindingName,
                daprTask.Operation,
                inputResponse.Data,
                metadata,
                cancellationToken: cancellationToken
            );

            stopwatch.Stop();
            
            // Record successful DAPR binding invocation
            workflowMetrics.RecordDaprBindingInvocation(daprTask.BindingName, daprTask.Operation, "success");
            
            Logger.LogInformation("DAPR binding task {TaskKey} completed successfully in {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);

            Logger.LogDebug("Processing output for DAPR binding task {TaskKey}", daprTask.Key);
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
            
            Logger.LogInformation("DAPR binding task {TaskKey} execution completed, returning processed output", daprTask.Key);
            return outputResponse.Data;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            
            // Record cancelled DAPR binding invocation
            workflowMetrics.RecordDaprBindingInvocation(daprTask.BindingName, daprTask.Operation, "cancelled");
            
            Logger.LogWarning("DAPR binding task {TaskKey} was cancelled after {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record failed DAPR binding invocation
            workflowMetrics.RecordDaprBindingInvocation(daprTask.BindingName, daprTask.Operation, "failure");
            
            Logger.LogError(ex, "Error occurred during DAPR binding task {TaskKey} execution after {Duration}ms - BindingName: {BindingName}, Operation: {Operation}", 
                daprTask.Key, stopwatch.ElapsedMilliseconds, daprTask.BindingName, daprTask.Operation);
            throw;
        }
    }
}