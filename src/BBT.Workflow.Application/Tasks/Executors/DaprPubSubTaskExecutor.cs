using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
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
/// <param name="workflowMetrics">The workflow metrics service for recording DAPR metrics.</param>
/// <param name="logger">The logger instance for logging DAPR pub/sub task execution details.</param>
public sealed class DaprPubSubTaskExecutor(
    IScriptEngine scriptEngine,
    DaprClient daprClient,
    IWorkflowMetrics workflowMetrics,
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
        
        StandardTaskResponse standardResponse;

        try
        {
            await PrepareInputAsync(daprTask, scriptCode, context, cancellationToken);
            await CallAsync(daprTask, context, cancellationToken);
            stopwatch.Stop();
            
            // Record successful DAPR pub/sub message published
            workflowMetrics.RecordDaprPubsubMessagePublished(daprTask.PubSubName, daprTask.Topic, "success");

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

            // Record cancelled DAPR pub/sub message publishing
            workflowMetrics.RecordDaprPubsubMessagePublished(daprTask.PubSubName, daprTask.Topic, "cancelled");

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

            // Record failed DAPR pub/sub message publishing
            workflowMetrics.RecordDaprPubsubMessagePublished(daprTask.PubSubName, daprTask.Topic, "failure");

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

        context.SetStandardResponse(standardResponse);
        var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
        return outputResponse;
    }
    public async Task CallAsync(WorkflowTask task, ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var daprTask = (task as DaprPubSubTask)!;
          var metadata = daprTask.Metadata.ValueKind != JsonValueKind.Null && daprTask.Metadata.ValueKind != JsonValueKind.Undefined
                ? daprTask.Metadata.ToDictionary()
                : new Dictionary<string, string>();
          
            await daprClient.PublishEventAsync(
                daprTask.PubSubName,
                daprTask.Topic,
                daprTask.Data,
                metadata,
                cancellationToken: cancellationToken
            );
    }

}