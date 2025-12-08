using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BBT.Workflow.Execution.Bindings;
using BBT.Workflow.Execution.Metrics;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Invokers;

/// <summary>
/// Remote invoker for DirectTrigger tasks.
/// Uses Dapr service invocation to call the orchestration app's transition endpoint.
/// Used for cross-domain transition triggering on existing workflow instances.
/// </summary>
public sealed class DirectTriggerRemoteInvoker(
    DaprClient daprClient,
    IConfiguration configuration,
    ILogger<DirectTriggerRemoteInvoker> logger,
    ITaskMetrics? metrics = null)
    : ITaskInvoker<DirectTriggerBinding>
{
    private readonly ITaskMetrics _metrics = metrics ?? NullTaskMetrics.Instance;
    private readonly string _orchestrationAppId = configuration["OrchestrationApi:AppId"] ?? "vnext-app";

    /// <inheritdoc />
    public string TaskType => TaskTypes.DirectTrigger;

    /// <inheritdoc />
    public Type BindingType => typeof(DirectTriggerBinding);

    /// <inheritdoc />
    public async Task<TaskInvocationResult> InvokeAsync(
        TaskDescriptor<DirectTriggerBinding> descriptor,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(descriptor.TaskKey, descriptor.Binding, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TaskInvocationResult> InvokeAsync(
        string? taskKey,
        JsonElement binding,
        CancellationToken cancellationToken = default)
    {
        var typedBinding = binding.Deserialize<DirectTriggerBinding>()
                           ?? throw new InvalidOperationException("Failed to deserialize DirectTriggerBinding");

        return await ExecuteAsync(taskKey, typedBinding, cancellationToken);
    }

    private async Task<TaskInvocationResult> ExecuteAsync(
        string? taskKey,
        DirectTriggerBinding binding,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrEmpty(binding.Identifier))
        {
            stopwatch.Stop();
            return TaskInvocationResult.Failure(
                error: "DirectTrigger task requires either instanceId or key to be specified",
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["InstanceId"] = string.Empty,
                    ["TransitionKey"] = binding.TransitionName,
                    ["OrchestrationAppId"] = _orchestrationAppId,
                    ["Cancelled"] = true
                });
        }
        
        try
        {
            // Build the endpoint path: /api/v1/{domain}/workflows/{workflow}/instances/{instanceId}/transitions/{transitionKey}
            var path =
                $"/api/v1/{binding.Domain}/workflows/{binding.Workflow}/instances/{binding.Identifier}/transitions/{binding.TransitionName}";

            // Add query parameters
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(binding.Version))
                queryParams.Add($"version={Uri.EscapeDataString(binding.Version)}");
            if (binding.Sync)
                queryParams.Add("sync=true");

            if (queryParams.Count > 0)
                path += "?" + string.Join("&", queryParams);

            // Build request body (transition data)
            var requestBody = new
            {
                key = binding.Key,
                tags = binding.Tags,
                attributes = binding.Body
            };

            var request = daprClient.CreateInvokeMethodRequest(
                HttpMethod.Patch,
                _orchestrationAppId,
                path);

            // Add body
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            // Add headers
            if (!string.IsNullOrEmpty(binding.Headers))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(binding.Headers);
                if (headers != null)
                {
                    foreach (var header in headers.Where(h => h.Value != null))
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            var response = await daprClient.InvokeMethodAsync<object?>(request, cancellationToken);
            stopwatch.Stop();

            _metrics.RecordTaskExecution(TaskType, "success");

            return TaskInvocationResult.Success(
                data: response,
                body: response != null ? JsonSerializer.Serialize(response) : null,
                statusCode: 200,
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["InstanceId"] = binding.Identifier,
                    ["TransitionKey"] = binding.TransitionName,
                    ["OrchestrationAppId"] = _orchestrationAppId
                });
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _metrics.RecordTaskExecution(TaskType, "cancelled");
            logger.LogWarning(
                "DirectTrigger remote invocation was cancelled for task {TaskKey}: {Domain}/{Workflow}/{InstanceId}/{TransitionKey}",
                taskKey, binding.Domain, binding.Workflow, binding.Identifier, binding.TransitionName);

            return TaskInvocationResult.Failure(
                error: "DirectTrigger remote invocation was cancelled",
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["InstanceId"] = binding.Identifier,
                    ["TransitionKey"] = binding.TransitionName,
                    ["OrchestrationAppId"] = _orchestrationAppId,
                    ["Cancelled"] = true
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordTaskExecution(TaskType, "failure");
            logger.LogError(ex,
                "DirectTrigger remote invocation failed for task {TaskKey}: {Domain}/{Workflow}/{InstanceId}/{TransitionKey}",
                taskKey, binding.Domain, binding.Workflow, binding.InstanceId, binding.TransitionName);

            return TaskInvocationResult.Failure(
                error: ex.Message,
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["InstanceId"] = binding.Identifier,
                    ["TransitionKey"] = binding.TransitionName,
                    ["OrchestrationAppId"] = _orchestrationAppId,
                    ["ExceptionType"] = ex.GetType().Name
                });
        }
    }
}