using System.Diagnostics;
using System.Text.Json;
using BBT.Workflow.Execution.Bindings;
using BBT.Workflow.Execution.Metrics;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Invokers;

/// <summary>
/// Remote invoker for GetInstanceData tasks.
/// Uses Dapr service invocation to call the orchestration app's instance data endpoint.
/// Used for cross-domain instance data retrieval.
/// </summary>
public sealed class GetInstanceDataRemoteInvoker(
    DaprClient daprClient,
    IConfiguration configuration,
    ILogger<GetInstanceDataRemoteInvoker> logger,
    ITaskMetrics? metrics = null)
    : ITaskInvoker<GetInstanceDataBinding>
{
    private readonly ITaskMetrics _metrics = metrics ?? NullTaskMetrics.Instance;
    private readonly string _orchestrationAppId = configuration["OrchestrationApi:AppId"] ?? "vnext-app";

    /// <inheritdoc />
    public string TaskType => TaskTypes.GetInstanceData;

    /// <inheritdoc />
    public Type BindingType => typeof(GetInstanceDataBinding);

    /// <inheritdoc />
    public async Task<TaskInvocationResult> InvokeAsync(
        TaskDescriptor<GetInstanceDataBinding> descriptor,
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
        var typedBinding = binding.Deserialize<GetInstanceDataBinding>()
            ?? throw new InvalidOperationException("Failed to deserialize GetInstanceDataBinding");

        return await ExecuteAsync(taskKey, typedBinding, cancellationToken);
    }

    private async Task<TaskInvocationResult> ExecuteAsync(
        string? taskKey,
        GetInstanceDataBinding binding,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the endpoint path: /api/v1/{domain}/workflows/{workflow}/instances/{instance}/data
            var path = $"/api/v1/{binding.Domain}/workflows/{binding.Workflow}/instances/{binding.Instance}/data";

            // Add query parameters for extensions
            if (binding.Extensions != null && binding.Extensions.Length > 0)
            {
                var extensionsParam = string.Join(",", binding.Extensions);
                path += $"?extensions={Uri.EscapeDataString(extensionsParam)}";
            }

            var request = daprClient.CreateInvokeMethodRequest(
                HttpMethod.Get,
                _orchestrationAppId,
                path);

            // Add ETag header for conditional request
            if (!string.IsNullOrEmpty(binding.ETag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", binding.ETag);
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
                    ["Instance"] = binding.Instance,
                    ["OrchestrationAppId"] = _orchestrationAppId
                });
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _metrics.RecordTaskExecution(TaskType, "cancelled");
            logger.LogWarning("GetInstanceData remote invocation was cancelled for task {TaskKey}: {Domain}/{Workflow}/{Instance}",
                taskKey, binding.Domain, binding.Workflow, binding.Instance);

            return TaskInvocationResult.Failure(
                error: "GetInstanceData remote invocation was cancelled",
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["Instance"] = binding.Instance,
                    ["OrchestrationAppId"] = _orchestrationAppId,
                    ["Cancelled"] = true
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordTaskExecution(TaskType, "failure");
            logger.LogError(ex, "GetInstanceData remote invocation failed for task {TaskKey}: {Domain}/{Workflow}/{Instance}",
                taskKey, binding.Domain, binding.Workflow, binding.Instance);

            return TaskInvocationResult.Failure(
                error: ex.Message,
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["Domain"] = binding.Domain,
                    ["Workflow"] = binding.Workflow,
                    ["Instance"] = binding.Instance,
                    ["OrchestrationAppId"] = _orchestrationAppId,
                    ["ExceptionType"] = ex.GetType().Name
                });
        }
    }
}

