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
/// Remote invoker for StartTrigger tasks.
/// Uses Dapr service invocation to call the orchestration app's workflow start endpoint.
/// Used for cross-domain workflow instance creation.
/// </summary>
public sealed class StartTriggerRemoteInvoker(
    DaprClient daprClient,
    IConfiguration configuration,
    ILogger<StartTriggerRemoteInvoker> logger,
    ITaskMetrics? metrics = null)
    : ITaskInvoker<StartTriggerBinding>
{
    private readonly ITaskMetrics _metrics = metrics ?? NullTaskMetrics.Instance;
    private readonly string _orchestrationAppId = configuration["OrchestrationApi:AppId"] ?? "vnext-app";

    /// <inheritdoc />
    public string TaskType => TaskTypes.StartTrigger;

    /// <inheritdoc />
    public Type BindingType => typeof(StartTriggerBinding);

    /// <inheritdoc />
    public async Task<TaskInvocationResult> InvokeAsync(
        TaskDescriptor<StartTriggerBinding> descriptor,
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
        var typedBinding = binding.Deserialize<StartTriggerBinding>()
            ?? throw new InvalidOperationException("Failed to deserialize StartTriggerBinding");

        return await ExecuteAsync(taskKey, typedBinding, cancellationToken);
    }

    private async Task<TaskInvocationResult> ExecuteAsync(
        string? taskKey,
        StartTriggerBinding binding,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the endpoint path: /api/v1/{domain}/workflows/{workflow}/instances/start
            var path = $"/api/v1/{binding.Domain}/workflows/{binding.Workflow}/instances/start";

            // Add query parameters
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(binding.Version))
                queryParams.Add($"version={Uri.EscapeDataString(binding.Version)}");
            if (binding.Sync)
                queryParams.Add("sync=true");

            if (queryParams.Count > 0)
                path += "?" + string.Join("&", queryParams);

            // Build request body
            var requestBody = new
            {
                key = binding.Key,
                tags = binding.Tags,
                attributes = binding.Body
            };

            var request = daprClient.CreateInvokeMethodRequest(
                HttpMethod.Post,
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
                    ["OrchestrationAppId"] = _orchestrationAppId
                });
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _metrics.RecordTaskExecution(TaskType, "cancelled");
            logger.LogWarning("StartTrigger remote invocation was cancelled for task {TaskKey}: {Domain}/{Workflow}",
                taskKey, binding.Domain, binding.Workflow);

            return TaskInvocationResult.Failure(
                error: "StartTrigger remote invocation was cancelled",
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["OrchestrationAppId"] = _orchestrationAppId,
                    ["Cancelled"] = true
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordTaskExecution(TaskType, "failure");
            logger.LogError(ex, "StartTrigger remote invocation failed for task {TaskKey}: {Domain}/{Workflow}",
                taskKey, binding.Domain, binding.Workflow);

            return TaskInvocationResult.Failure(
                error: ex.Message,
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["OrchestrationAppId"] = _orchestrationAppId,
                    ["ExceptionType"] = ex.GetType().Name
                });
        }
    }
}

