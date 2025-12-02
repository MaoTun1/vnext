using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Scripting;
using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes DAPR HTTP endpoint workflow tasks that invoke external HTTP endpoints through DAPR runtime.
/// This executor facilitates HTTP communication using DAPR's HTTP endpoint invocation capabilities.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="daprClient">The DAPR client for making HTTP endpoint invocations.</param>
/// <param name="workflowMetrics">The workflow metrics service for recording DAPR metrics.</param>
/// <param name="logger">The logger instance for logging DAPR HTTP endpoint task execution details.</param>
public sealed class DaprHttpEndpointTaskExecutor(
    IScriptEngine scriptEngine,
    DaprClient daprClient,
    IWorkflowMetrics workflowMetrics,
    ILogger<DaprHttpEndpointTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a DAPR HTTP endpoint task by preparing the request through script mapping, invoking the HTTP endpoint,
    /// and processing the response through output mapping.
    /// </summary>
    /// <param name="task">The DAPR HTTP endpoint workflow task containing endpoint name, path, and method configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing for the HTTP call.</param>
    /// <param name="context">The script context containing data for request preparation and response handling.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the HTTP endpoint invocation after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to DaprHttpEndpointTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var daprTask = (task as DaprHttpEndpointTask)!;
        try
        {
            await PrepareInputAsync(daprTask, scriptCode, context, cancellationToken);
            
            //Usage
            var response = await daprClient.InvokeMethodAsync<object?, object>(
                new HttpMethod(daprTask.Method),
                daprTask.EndpointName,
                daprTask.Path,
                daprTask.Body,
                cancellationToken: cancellationToken
            );
            
            // Record successful DAPR service invocation (HTTP endpoint)
            workflowMetrics.RecordDaprServiceInvocation(daprTask.EndpointName, daprTask.Path, "success");
            context.SetBody(response);
            
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
            
            return outputResponse;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            // Record cancelled DAPR service invocation (HTTP endpoint)
            workflowMetrics.RecordDaprServiceInvocation(daprTask.EndpointName, daprTask.Path, "cancelled");
            throw;
        }
        catch (Exception ex)
        {
            // Record failed DAPR service invocation (HTTP endpoint)
            workflowMetrics.RecordDaprServiceInvocation(daprTask.EndpointName, daprTask.Path, "failure");
            
            Logger.LogError(ex, "Error occurred during DAPR HTTP endpoint task {TaskKey} execution after - EndpointName: {EndpointName}, Path: {Path}", 
                daprTask.Key,  daprTask.EndpointName, daprTask.Path);
            throw;
        }
    }
}