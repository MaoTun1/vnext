using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes DAPR publish/subscribe workflow tasks that publish messages to topics through DAPR runtime.
/// This executor facilitates asynchronous messaging using DAPR's pub/sub building block for event-driven communication.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="daprClient">The DAPR client for publishing messages to pub/sub topics.</param>
/// <param name="logger">The logger instance for logging DAPR pub/sub task execution details.</param>
public sealed class DaprPubSubTaskExecutor(
    IScriptEngine scriptEngine,
    DaprClient daprClient,
    ILogger<DaprPubSubTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a DAPR pub/sub task by preparing the message through script mapping, publishing to the specified topic,
    /// and processing any output through output mapping.
    /// </summary>
    /// <param name="task">The DAPR pub/sub workflow task containing pub/sub name, topic, and metadata configuration.</param>
    /// <param name="scriptCode">The script code that handles message preparation and output processing for the publication.</param>
    /// <param name="context">The script context containing data for message preparation and processing.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed data
    /// from the output mapping after message publication.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to DaprPubSubTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var daprTask = (task as DaprPubSubTask)!;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Logger.LogInformation("Starting DAPR pub/sub task execution for task {TaskKey} - PubSubName: {PubSubName}, Topic: {Topic}", 
            daprTask.Key, daprTask.PubSubName, daprTask.Topic);
            
        StandardTaskResponse standardResponse;

        try
        {
            Logger.LogDebug("Preparing input for DAPR pub/sub task {TaskKey}", daprTask.Key);
            var inputResponse = await PrepareInputAsync(daprTask, scriptCode, context, cancellationToken);

            //Usage
            var metadata = daprTask.Metadata.ValueKind != JsonValueKind.Null && daprTask.Metadata.ValueKind != JsonValueKind.Undefined 
                ? daprTask.Metadata.ToDictionary() 
                : new Dictionary<string, string>();
            
            Logger.LogDebug("Prepared metadata for DAPR pub/sub task {TaskKey}: {MetadataCount} entries", 
                daprTask.Key, metadata.Count);
            
            if (metadata.Count > 0)
            {
                Logger.LogDebug("DAPR pub/sub metadata for task {TaskKey}: {Metadata}", 
                    daprTask.Key, string.Join(", ", metadata.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            Logger.LogInformation("Publishing event to DAPR pub/sub for task {TaskKey}: {PubSubName}/{Topic}", 
                daprTask.Key, daprTask.PubSubName, daprTask.Topic);
                
            await daprClient.PublishEventAsync(
                daprTask.PubSubName,
                daprTask.Topic,
                daprTask.Data,
                metadata,
                cancellationToken: cancellationToken
            );

            stopwatch.Stop();

            Logger.LogInformation("DAPR pub/sub task {TaskKey} completed successfully in {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);

            standardResponse = CreateSuccessResponse(
                data: new { Published = true, Message = "Event published successfully" },
                taskType: nameof(TaskType.DaprPubSub),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                metadata: new Dictionary<string, object>
                {
                    ["PubSubName"] = daprTask.PubSubName,
                    ["Topic"] = daprTask.Topic
                });
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Logger.LogWarning("DAPR pub/sub task {TaskKey} was cancelled after {Duration}ms", 
                daprTask.Key, stopwatch.ElapsedMilliseconds);
                
            standardResponse = CreateErrorResponse(
                errorMessage: "DAPR pub/sub operation was cancelled",
                taskType: nameof(TaskType.DaprPubSub),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["PubSubName"] = daprTask.PubSubName,
                    ["Topic"] = daprTask.Topic,
                    ["Cancelled"] = true
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error occurred during DAPR pub/sub task {TaskKey} execution after {Duration}ms - PubSubName: {PubSubName}, Topic: {Topic}", 
                daprTask.Key, stopwatch.ElapsedMilliseconds, daprTask.PubSubName, daprTask.Topic);
                
            standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.DaprPubSub),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["PubSubName"] = daprTask.PubSubName,
                    ["Topic"] = daprTask.Topic
                });
        }

        Logger.LogDebug("Setting standard response in context for DAPR pub/sub task {TaskKey}", daprTask.Key);
        context.SetStandardResponse(standardResponse);
        
        Logger.LogDebug("Processing output for DAPR pub/sub task {TaskKey}", daprTask.Key);
        var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
        
        Logger.LogInformation("DAPR pub/sub task {TaskKey} execution completed, returning processed output", daprTask.Key);
        return outputResponse.Data;
    }
}