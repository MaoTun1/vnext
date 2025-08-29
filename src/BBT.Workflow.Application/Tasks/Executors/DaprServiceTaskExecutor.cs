using System.Text;
using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Scripting;
using Dapr;
using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes DAPR service invocation workflow tasks that call methods on remote services through DAPR runtime.
/// This executor facilitates service-to-service communication using DAPR's service invocation building block.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="daprClient">The DAPR client for making service invocation calls to remote applications.</param>
/// <param name="workflowMetrics">The workflow metrics service for recording DAPR metrics.</param>
/// <param name="logger">The logger instance for logging DAPR service task execution details.</param>
public sealed class DaprServiceTaskExecutor(
    IScriptEngine scriptEngine,
    DaprClient daprClient,
    IWorkflowMetrics workflowMetrics,
    ILogger<DaprServiceTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a DAPR service task by preparing the request through script mapping, invoking the remote service method,
    /// and processing the response through output mapping.
    /// </summary>
    /// <param name="task">The DAPR service workflow task containing app ID, method name, and HTTP verb configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing for the service call.</param>
    /// <param name="context">The script context containing data for request preparation and response handling.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the DAPR service invocation after output mapping transformation.
    /// </returns>
    /// <exception cref="DaprException">Thrown when the DAPR service invocation fails or the target service is unavailable.</exception>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to DaprServiceTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var daprTask = (task as DaprServiceTask)!;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Logger.LogInformation("Starting DAPR service task execution for task {TaskKey} - AppId: {AppId}, Method: {MethodName}, Verb: {HttpVerb}", 
            daprTask.Key, daprTask.AppId, daprTask.MethodName, daprTask.HttpVerb);
            
        StandardTaskResponse standardResponse;

        try
        {
            Logger.LogDebug("Preparing input for DAPR service task {TaskKey}", daprTask.Key);
            await PrepareInputAsync(daprTask, scriptCode, context, cancellationToken);

            Logger.LogDebug("Creating DAPR service invocation request for AppId: {AppId}, Method: {MethodName}", 
                daprTask.AppId, daprTask.MethodName);
                
            var request = daprClient.CreateInvokeMethodRequest(
                new HttpMethod(daprTask.HttpVerb),
                daprTask.AppId,
                daprTask.MethodName);

            if (request.Method != HttpMethod.Get && daprTask.Data.HasValue)
            {
                var requestContent = daprTask.Data.Value.GetRawText();
                request.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");
                
                Logger.LogDebug("Added request body to DAPR service call for task {TaskKey}", daprTask.Key);
            }

            Logger.LogInformation("Invoking DAPR service for task {TaskKey}: {AppId}/{MethodName} via {HttpVerb}", 
                daprTask.Key, daprTask.AppId, daprTask.MethodName, daprTask.HttpVerb);

            var response = await daprClient.InvokeMethodAsync<object?>(
                request,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            // Record successful DAPR service invocation
            workflowMetrics.RecordDaprServiceInvocation(daprTask.AppId, daprTask.MethodName, "success");

            Logger.LogInformation("DAPR service task {TaskKey} completed successfully in {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);

            standardResponse = CreateSuccessResponse(
                data: response,
                taskType: "DaprServiceTask",
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                metadata: new Dictionary<string, object>
                {
                    ["AppId"] = daprTask.AppId,
                    ["MethodName"] = daprTask.MethodName,
                    ["HttpVerb"] = daprTask.HttpVerb
                });
        }
        catch (DaprException ex)
        {
            stopwatch.Stop();
            
            // Record failed DAPR service invocation
            workflowMetrics.RecordDaprServiceInvocation(daprTask.AppId, daprTask.MethodName, "failure");
            
            Logger.LogError(ex, "DAPR service invocation failed for task {TaskKey} - AppId: {AppId}, Method: {MethodName}, Duration: {Duration}ms", 
                daprTask.Key, daprTask.AppId, daprTask.MethodName, stopwatch.ElapsedMilliseconds);
                
            standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.DaprService),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["AppId"] = daprTask.AppId,
                    ["MethodName"] = daprTask.MethodName,
                    ["HttpVerb"] = daprTask.HttpVerb,
                });
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            
            // Record cancelled DAPR service invocation
            workflowMetrics.RecordDaprServiceInvocation(daprTask.AppId, daprTask.MethodName, "cancelled");
            
            Logger.LogWarning("DAPR service task {TaskKey} was cancelled after {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);
                
            standardResponse = CreateErrorResponse(
                errorMessage: "DAPR service invocation was cancelled",
                taskType: nameof(TaskType.DaprService),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["AppId"] = daprTask.AppId,
                    ["MethodName"] = daprTask.MethodName,
                    ["HttpVerb"] = daprTask.HttpVerb,
                    ["Cancelled"] = true
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record failed DAPR service invocation
            workflowMetrics.RecordDaprServiceInvocation(daprTask.AppId, daprTask.MethodName, "failure");
            
            Logger.LogError(ex, "Unexpected error occurred during DAPR service task {TaskKey} execution after {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);
                
            standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.DaprService),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["AppId"] = daprTask.AppId,
                    ["MethodName"] = daprTask.MethodName,
                    ["HttpVerb"] = daprTask.HttpVerb
                });
        }

        Logger.LogDebug("Setting standard response in context for DAPR service task {TaskKey}", daprTask.Key);
        context.SetStandardResponse(standardResponse);
        
        Logger.LogDebug("Processing output for DAPR service task {TaskKey}", daprTask.Key);
        var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
        
        Logger.LogInformation("DAPR service task {TaskKey} execution completed, returning processed output", daprTask.Key);
        return outputResponse;
    }
}