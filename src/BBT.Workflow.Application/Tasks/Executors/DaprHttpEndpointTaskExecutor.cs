using BBT.Workflow.Definitions;
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
/// <param name="logger">The logger instance for logging DAPR HTTP endpoint task execution details.</param>
public sealed class DaprHttpEndpointTaskExecutor(
    IScriptEngine scriptEngine,
    DaprClient daprClient,
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Logger.LogInformation("Starting DAPR HTTP endpoint task execution for task {TaskKey} - EndpointName: {EndpointName}, Path: {Path}, Method: {Method}", 
            daprTask.Key, daprTask.EndpointName, daprTask.Path, daprTask.Method);

        try
        {
            Logger.LogDebug("Preparing input for DAPR HTTP endpoint task {TaskKey}", daprTask.Key);
            await PrepareInputAsync(daprTask, scriptCode, context, cancellationToken);

            Logger.LogInformation("Invoking DAPR HTTP endpoint for task {TaskKey}: {EndpointName}{Path} via {Method}", 
                daprTask.Key, daprTask.EndpointName, daprTask.Path, daprTask.Method);

            //Usage
            var response = await daprClient.InvokeMethodAsync<object?, object>(
                new HttpMethod(daprTask.Method),
                daprTask.EndpointName,
                daprTask.Path,
                daprTask.Body,
                cancellationToken: cancellationToken
            );

            stopwatch.Stop();
            Logger.LogInformation("DAPR HTTP endpoint task {TaskKey} completed successfully in {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);

            Logger.LogDebug("Setting response body in context for DAPR HTTP endpoint task {TaskKey}", daprTask.Key);
            context.SetBody(response);
            
            Logger.LogDebug("Processing output for DAPR HTTP endpoint task {TaskKey}", daprTask.Key);
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
            
            Logger.LogInformation("DAPR HTTP endpoint task {TaskKey} execution completed, returning processed output", daprTask.Key);
            return outputResponse.Data;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Logger.LogWarning("DAPR HTTP endpoint task {TaskKey} was cancelled after {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error occurred during DAPR HTTP endpoint task {TaskKey} execution after {Duration}ms - EndpointName: {EndpointName}, Path: {Path}", 
                daprTask.Key, stopwatch.ElapsedMilliseconds, daprTask.EndpointName, daprTask.Path);
            throw;
        }
    }
}