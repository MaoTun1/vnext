using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Notifications;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BBT.Workflow.Application.Notifications;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes notification workflow tasks that send instance state updates through notification senders.
/// This executor sends workflow instance state information through configured notification channels (SignalR, MQTT, etc.).
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="configuration">The application configuration to read binding component settings.</param>
/// <param name="runtimeInfoProvider">The runtime information provider for domain checks.</param>
/// <param name="instanceCorrelationRepository">The instance correlation repository for retrieving active correlations.</param>
/// <param name="componentDetector">Detector that resolves Dapr component type and notification type.</param>
/// <param name="taskExecutorFactory">Factory for creating task executors.</param>
/// <param name="logger">The logger instance for logging notification task execution details.</param>
public sealed class NotificationTaskExecutor(
    IScriptEngine scriptEngine,
    IConfiguration configuration,
    IRuntimeInfoProvider runtimeInfoProvider,
    IInstanceCorrelationRepository instanceCorrelationRepository,
    DaprComponentDetector componentDetector,
    ITaskExecutorFactory taskExecutorFactory,
    ILogger<NotificationTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a notification task by building the instance state output and sending it through the notification sender.
    /// </summary>
    /// <param name="task">The notification workflow task containing configuration details.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the notification sender after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to NotificationTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var notificationTask = (task as NotificationTask)!;

        try
        {
            // Check runtime domain
            runtimeInfoProvider.Check(context.Runtime.Domain);

            // Build instance state output
            var instanceOutput = await BuildInstanceStateOutputAsync(notificationTask, context, cancellationToken);

            // Create notification message
            var notificationMessage = NotificationMessage.Create(
                id: context.Instance.Id.ToString(),
                data: instanceOutput);

            // Determine component name precedence: task metadata -> configuration
            var componentName = TryGetFromMetadata(notificationTask, "componentName")
                                ?? configuration["DaprNotification:ComponentName"];

            if (string.IsNullOrWhiteSpace(componentName))
                throw new InvalidOperationException(
                    "ComponentName must be provided in task metadata or configuration.");

            // Prepare metadata (without headers)
            var metadata = new Dictionary<string, string>();

            // Merge any metadata from task.config.metadata (string pairs)
            foreach (var kv in GetStringDictionary(notificationTask.Metadata))
            {
                if (!metadata.ContainsKey(kv.Key))
                    metadata[kv.Key] = kv.Value;
            }

            var (_, notificationType, updatedMetadata) =
                await componentDetector.DetectAsync(componentName!, metadata, cancellationToken);


            // Route to appropriate executor based on notification component type
            if (notificationType == NotificationComponentType.HttpBinding)
            {
                // Create DaprBindingTask and call DaprBindingTaskExecutor
                var daprBindingTask = CreateDaprBindingTask(notificationTask, componentName!, notificationMessage,
                    updatedMetadata);
                var bindingExecutor = taskExecutorFactory.GetExecutor(TaskType.DaprBinding) as DaprBindingTaskExecutor;

                if (bindingExecutor == null)
                    throw new InvalidOperationException("DaprBindingTaskExecutor not found");

                await bindingExecutor.CallAsync(daprBindingTask, context, cancellationToken);
            }
            else if (notificationType == NotificationComponentType.PubSub)
            {
                // Create DaprPubSubTask and call DaprPubSubTaskExecutor
                var daprPubSubTask = CreateDaprPubSubTask(notificationTask, componentName!, notificationMessage,
                    updatedMetadata);
                var pubSubExecutor = taskExecutorFactory.GetExecutor(TaskType.DaprPubSub) as DaprPubSubTaskExecutor;

                if (pubSubExecutor == null)
                    throw new InvalidOperationException("DaprPubSubTaskExecutor not found");

                await pubSubExecutor.CallAsync(daprPubSubTask, context, cancellationToken);
            }
            else if (notificationType == NotificationComponentType.MqttBinding)
            {
                // Create DaprBindingTask and call DaprBindingTaskExecutor for MQTT
                var daprBindingTask = CreateDaprBindingTask(notificationTask, componentName!, notificationMessage,
                    updatedMetadata);
                var bindingExecutor = taskExecutorFactory.GetExecutor(TaskType.DaprBinding) as DaprBindingTaskExecutor;

                if (bindingExecutor == null)
                    throw new InvalidOperationException("DaprBindingTaskExecutor not found");

                await bindingExecutor.CallAsync(daprBindingTask, context, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during notification task {TaskKey} execution", notificationTask.Key);

            StandardTaskResponse standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.Notification),
                executionDurationMs: 0,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["TaskKey"] = notificationTask.Key
                });
            context.SetStandardResponse(standardResponse);
        }

        return new ScriptResponse()
        {
            Headers = context.Headers,
            Data = context.Body
        };
    }

    private async Task<object> BuildInstanceStateOutputAsync(
        NotificationTask notificationTask,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        var workflow = context.Workflow;
        var instance = context.Instance;

        // Build instance transition information
        var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

        // Get available transitions
        var availableTransitions = GetMainFlowTransitions(instance, workflow, transitionInfo);

        // Build transition items with href links
        var transitionItems = availableTransitions.Select(transitionKey => new TransitionItem
        {
            Name = transitionKey,
            Href = InstanceUrlTemplates.Transition(context.Runtime.Domain, workflow.Key, instance.Id.ToString(),
                transitionKey)
        }).ToList();

        // Build data href
        var dataHref = new DataHref
        {
            Href = InstanceUrlTemplates.Data(context.Runtime.Domain, workflow.Key, instance.Id.ToString())
        };

        // Build view href
        var viewHref = new ViewHref
        {
            Href = InstanceUrlTemplates.View(context.Runtime.Domain, workflow.Key, instance.Id.ToString()),
            LoadData = true
        };

        // Build active correlations with href links
        var activeCorrelations = transitionInfo.ActiveCorrelations.Select(correlation => new ActiveCorrelationHref
        {
            CorrelationId = correlation.CorrelationId,
            ParentState = correlation.ParentState,
            SubFlowInstanceId = correlation.SubFlowInstanceId,
            SubFlowType = correlation.SubFlowType,
            SubFlowDomain = correlation.SubFlowDomain,
            SubFlowName = correlation.SubFlowName,
            SubFlowVersion = correlation.SubFlowVersion,
            IsCompleted = correlation.IsCompleted,
            Href = InstanceUrlTemplates.Data(correlation.SubFlowDomain, correlation.SubFlowName,
                correlation.SubFlowInstanceId.ToString())
        }).ToList();

        return new GetInstanceStateOutput
        {
            Data = dataHref,
            View = viewHref,
            State = context.Instance.CurrentState ?? string.Empty,
            Status = context.Instance.Status,
            ActiveCorrelations = activeCorrelations,
            Transitions = transitionItems,
            ETag = context.Instance.LatestData?.ETag ?? string.Empty
        };
    }

    private async Task<(InstanceStatus Status, string? CurrentState, List<InstanceCorrelationInfo> ActiveCorrelations)>
        BuildInstanceTransitionInfoAsync(Instance instance, CancellationToken cancellationToken)
    {
        var correlations = await instanceCorrelationRepository.GetActiveByParentAsync(instance.Id, cancellationToken);

        var activeCorrelations = correlations
            .Select(c => new InstanceCorrelationInfo
            {
                CorrelationId = c.Id,
                ParentState = c.ParentState,
                SubFlowInstanceId = c.SubFlowInstanceId,
                SubFlowType = c.SubFlowType,
                SubFlowDomain = c.SubFlowDomain,
                SubFlowName = c.SubFlowName,
                SubFlowVersion = c.SubFlowVersion,
                IsCompleted = c.IsCompleted
            })
            .ToList();

        return (instance.Status, instance.CurrentState, activeCorrelations);
    }

    private List<string> GetMainFlowTransitions(
        Instance instance,
        BBT.Workflow.Definitions.Workflow currentWorkflow,
        (InstanceStatus Status, string? CurrentState, List<InstanceCorrelationInfo> ActiveCorrelations) transitionInfo)
    {
        var availableTransitions = new List<string>();

        if (instance.Status.Equals(InstanceStatus.Active))
        {
            var stateResult = currentWorkflow.GetState(instance.GetCurrentState);
            if (stateResult.IsSuccess)
            {
                availableTransitions = currentWorkflow.GetAvailableUserTransitionKeys(stateResult.Value!);
            }
        }

        return availableTransitions;
    }

    private static string? TryGetFromMetadata(NotificationTask notificationTask, string key)
    {
        if (notificationTask.Metadata.HasValue)
        {
            var el = notificationTask.Metadata.Value;
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v) &&
                v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }

        return null;
    }

    private static Dictionary<string, string> GetStringDictionary(JsonElement? element)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!element.HasValue)
            return dict;
        var el = element.Value;
        if (el.ValueKind != JsonValueKind.Object)
            return dict;
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString() ?? string.Empty;
                dict[prop.Name] = val;
            }
        }

        return dict;
    }

    /// <summary>
    /// Creates a DaprBindingTask from notification task data for HTTP binding handlers.
    /// </summary>
    private DaprBindingTask CreateDaprBindingTask(
        NotificationTask notificationTask,
        string componentName,
        NotificationMessage notificationMessage,
        Dictionary<string, string> metadata)
    {
        // Get operation from metadata (method), default to "create"
        var operation = metadata.TryGetValue("method", out var method) && !string.IsNullOrWhiteSpace(method)
            ? method
            : "create";

        // Prepare metadata dictionary (will be processed in DaprBindingTaskExecutor)
        // Include ForwardingHeaders and method for processing
        // Context headers will be read directly from ScriptContext in DaprBindingTaskExecutor
        var taskMetadata = new Dictionary<string, string>(metadata);

        // Create task config JSON
        var configBuilder = new Dictionary<string, object>
        {
            ["key"] = notificationTask.Key,
            ["bindingName"] = componentName,
            ["operation"] = operation,
            ["metadata"] = taskMetadata,
            ["data"] = JsonSerializer.SerializeToElement(notificationMessage)
        };

        // Copy base properties from notificationTask if available
        var configJson = JsonSerializer.Serialize(configBuilder);
        var configElement = JsonDocument.Parse(configJson).RootElement;

        var daprBindingTask = DaprBindingTask.Create(configElement);

        // Copy base properties from notificationTask
        notificationTask.CopyBaseToInternal(daprBindingTask);

        return daprBindingTask;
    }

    /// <summary>
    /// Creates a DaprPubSubTask from notification task data for Kafka/pubsub handlers.
    /// </summary>
    private DaprPubSubTask CreateDaprPubSubTask(
        NotificationTask notificationTask,
        string componentName,
        NotificationMessage notificationMessage,
        Dictionary<string, string> metadata)
    {
        // Get topic from metadata, default to "notifications"
        var topic = metadata.TryGetValue("topic", out var t) && !string.IsNullOrWhiteSpace(t)
            ? t
            : "notifications";

        // Create task config JSON
        var configBuilder = new Dictionary<string, object>
        {
            ["key"] = notificationTask.Key,
            ["pubSubName"] = componentName,
            ["topic"] = topic,
            ["metadata"] = metadata,
            ["data"] = JsonSerializer.SerializeToElement(notificationMessage)
        };

        var configJson = JsonSerializer.Serialize(configBuilder);
        var configElement = JsonDocument.Parse(configJson).RootElement;

        var daprPubSubTask = DaprPubSubTask.Create(configElement);

        // Copy base properties from notificationTask
        notificationTask.CopyBaseToInternal(daprPubSubTask);

        return daprPubSubTask;
    }
}