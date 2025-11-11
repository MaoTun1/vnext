using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes trigger transition workflow tasks that trigger transitions on workflow instances.
/// This executor can create new instances, trigger direct transitions, or trigger transitions on correlated SubFlow instances.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="instanceCorrelationRepository">The instance correlation repository for retrieving active correlations.</param>
/// <param name="runtimeInfoProvider">The runtime information provider for domain checks.</param>
/// <param name="taskExecutorFactory">Factory for creating task executors.</param>
/// <param name="configuration">The configuration instance for reading vNextApi settings.</param>
/// <param name="logger">The logger instance for logging trigger transition task execution details.</param>
public sealed class TriggerTransitionTaskExecutor(
    IScriptEngine scriptEngine,
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IRuntimeInfoProvider runtimeInfoProvider,
    ITaskExecutorFactory taskExecutorFactory,
    IConfiguration configuration,
    ILogger<TriggerTransitionTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a trigger transition task by preparing the request through script mapping, triggering the appropriate transition,
    /// and processing the response through output mapping.
    /// </summary>
    /// <param name="task">The trigger transition workflow task containing transition configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the transition execution after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to TriggerTransitionTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var triggerTask = (task as TriggerTransitionTask)!;

        Logger.LogInformation("Starting trigger transition task execution for task {TaskKey} - TriggerType: {TriggerType}, Domain: {Domain}, Flow: {Flow}",
            triggerTask.Key, triggerTask.TriggerType, triggerTask.TransitionDomain, triggerTask.TransitionFlow);

        try
        {
            // Check runtime domain
            runtimeInfoProvider.Check(context.Runtime.Domain);

            Logger.LogDebug("Preparing input for trigger transition task {TaskKey}", triggerTask.Key);
            await PrepareInputAsync(triggerTask, scriptCode, context, cancellationToken);

            // Route to appropriate handler based on trigger type
            switch (triggerTask.TriggerType)
            {
                case TriggerTransitionType.Start:
                    await HandleCreateNewAsync(triggerTask, context, cancellationToken);
                    break;

                case TriggerTransitionType.Trigger:
                    await HandleDirectAsync(triggerTask, context, cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported trigger type: {triggerTask.TriggerType}");
            }

            Logger.LogDebug("Processing output for trigger transition task {TaskKey}", triggerTask.Key);
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);

            Logger.LogInformation("Trigger transition task {TaskKey} execution completed, returning processed output", triggerTask.Key);
            return outputResponse;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during trigger transition task {TaskKey} execution - TriggerType: {TriggerType}, Domain: {Domain}, Flow: {Flow}",
                triggerTask.Key, triggerTask.TriggerType, triggerTask.TransitionDomain, triggerTask.TransitionFlow);

            StandardTaskResponse standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.TriggerTransition),
                executionDurationMs: 0,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["TaskKey"] = triggerTask.Key,
                    ["TriggerType"] = triggerTask.TriggerType.ToString(),
                    ["Domain"] = triggerTask.TransitionDomain,
                    ["Flow"] = triggerTask.TransitionFlow
                });
            context.SetStandardResponse(standardResponse);
            throw;
        }
    }

    /// <summary>
    /// Handles CreateNew trigger type by creating a new workflow instance using HttpTask.
    /// </summary>
    private async Task HandleCreateNewAsync(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Handling CreateNew trigger for task {TaskKey} - Domain: {Domain}, Flow: {Flow}",
            triggerTask.Key, triggerTask.TransitionDomain, triggerTask.TransitionFlow);

        // Create path using format: /{domain}/workflows/{workflow}/instances/start
        var path = string.Format("/{0}/workflows/{1}/instances/start",
            triggerTask.TransitionDomain,
            triggerTask.TransitionFlow);

        var httpTask = CreateHttpTask(triggerTask, context, path, "POST");

        var httpExecutor = taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");

        Logger.LogDebug("Calling HttpTaskExecutor.CallAsync for CreateNew trigger task {TaskKey}", triggerTask.Key);
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }

    /// <summary>
    /// Handles Direct trigger type by executing a transition on the current instance using HttpTask.
    /// </summary>
    private async Task HandleDirectAsync(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(triggerTask.TransitionName))
            throw new InvalidOperationException("TransitionName is required for Direct trigger type");

        Logger.LogInformation("Handling Direct trigger for task {TaskKey} - InstanceId: {InstanceId}, Transition: {Transition}",
            triggerTask.Key, context.Instance.Id, triggerTask.TransitionName);

        // Create path using InstanceUrlTemplates.Transition format
        var path = string.Format(InstanceUrlTemplates.Transition,
            triggerTask.TransitionDomain,
            triggerTask.TransitionFlow,
            context.Instance.Id,
            triggerTask.TransitionName);

        var httpTask = CreateHttpTask(triggerTask, context, path, "PATCH");

        var httpExecutor = taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");

        Logger.LogDebug("Calling HttpTaskExecutor.CallAsync for Direct trigger task {TaskKey}", triggerTask.Key);
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }

    /// <summary>
    /// Handles Correlation trigger type by finding the appropriate SubFlow instance and executing a transition using HttpTask.
    /// </summary>
    private async Task HandleCorrelationAsync(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(triggerTask.TransitionName))
            throw new InvalidOperationException("TransitionName is required for Correlation trigger type");

        Logger.LogInformation("Handling Correlation trigger for task {TaskKey} - ParentInstanceId: {InstanceId}, SubFlowName: {SubFlowName}",
            triggerTask.Key, context.Instance.Id, triggerTask.SubFlowName);

        // Get active correlations for the parent instance
        var activeCorrelations = await instanceCorrelationRepository.GetActiveByParentAsync(
            context.Instance.Id,
            cancellationToken);

        if (activeCorrelations == null || activeCorrelations.Count == 0)
            throw new InvalidOperationException($"No active correlations found for parent instance {context.Instance.Id}");

        // Find matching SubFlow instance
        Guid subFlowInstanceId;
        if (!string.IsNullOrWhiteSpace(triggerTask.SubFlowName))
        {
            // Try to find correlation matching SubFlowName
            var matchingCorrelation = activeCorrelations.FirstOrDefault(c =>
                string.Equals(c.SubFlowName, triggerTask.SubFlowName, StringComparison.OrdinalIgnoreCase));

            if (matchingCorrelation != null)
            {
                subFlowInstanceId = matchingCorrelation.SubFlowInstanceId;
                Logger.LogDebug("Found matching SubFlow correlation for SubFlowName: {SubFlowName}, InstanceId: {InstanceId}",
                    triggerTask.SubFlowName, subFlowInstanceId);
            }
            else
            {
                // Use first active correlation if no match found
                subFlowInstanceId = activeCorrelations[0].SubFlowInstanceId;
                Logger.LogWarning("No matching SubFlow found for SubFlowName: {SubFlowName}, using first active correlation InstanceId: {InstanceId}",
                    triggerTask.SubFlowName, subFlowInstanceId);
            }
        }
        else
        {
            // Use first active correlation if SubFlowName not specified
            subFlowInstanceId = activeCorrelations[0].SubFlowInstanceId;
            Logger.LogDebug("SubFlowName not specified, using first active correlation InstanceId: {InstanceId}", subFlowInstanceId);
        }

        // Create path using InstanceUrlTemplates.Transition format
        var path = string.Format(InstanceUrlTemplates.Transition,
            triggerTask.TransitionDomain,
            triggerTask.TransitionFlow,
            subFlowInstanceId,
            triggerTask.TransitionName);

        var httpTask = CreateHttpTask(triggerTask, context, path, "PATCH");

        var httpExecutor = taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");

        Logger.LogDebug("Calling HttpTaskExecutor.CallAsync for Correlation trigger task {TaskKey}", triggerTask.Key);
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }

    /// <summary>
    /// Creates an HttpTask for calling workflow instance endpoints.
    /// </summary>
    /// <param name="triggerTask">The trigger transition task containing configuration.</param>
    /// <param name="context">The script context containing headers and body data.</param>
    /// <param name="path">The API endpoint path to call (without base URL and version).</param>
    /// <param name="method">The HTTP method (POST, PATCH, etc.).</param>
    /// <returns>A configured HttpTask ready to execute.</returns>
    private HttpTask CreateHttpTask(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        string path,
        string method)
    {
        // Build full URL using configuration
        var baseUrl = configuration["vNextApi:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
        var apiVersion = configuration["vNextApi:ApiVersion"] ?? "1";
        var fullUrl = $"{baseUrl}/api/v{apiVersion}{path}";

        Logger.LogDebug("Creating HttpTask with URL: {Url}", fullUrl);

        // Prepare body from triggerTask.Body or context.Body
        JsonElement? body = triggerTask.Body;
        if (!body.HasValue && context.Body != null)
        {
            body = JsonSerializer.SerializeToElement(context.Body);
        }

        // Prepare headers from context.Headers
        var headersDict = new Dictionary<string, string>();
        if (context.Headers != null)
        {
            foreach (var header in context.Headers)
            {
                var key = header.Key?.ToString() ?? string.Empty;
                var value = header.Value?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    headersDict[key] = value;
                }
            }
        }

        // Get timeout from configuration
        var timeoutSeconds = configuration.GetValue<int>("vNextApi:TimeoutSeconds", 30);

        // Create task config JSON
        var configBuilder = new Dictionary<string, object>
        {
            ["key"] = triggerTask.Key,
            ["url"] = fullUrl,
            ["method"] = method,
            ["timeoutSeconds"] = timeoutSeconds,
            ["validateSSL"] = true
        };

        if (headersDict.Count > 0)
        {
            configBuilder["headers"] = headersDict;
        }

        if (body.HasValue)
        {
            configBuilder["body"] = body.Value;
        }

        var configJson = JsonSerializer.Serialize(configBuilder);
        var configElement = JsonDocument.Parse(configJson).RootElement;

        var httpTask = HttpTask.Create(configElement);

        // Copy base properties from triggerTask
        triggerTask.CopyBaseToInternal(httpTask);

        return httpTask;
    }
}

