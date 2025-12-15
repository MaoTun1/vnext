using System.Diagnostics;
using System.Text.Json;
using BBT.Workflow.Execution.Bindings;
using BBT.Workflow.Execution.Metrics;
using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Invokers;

/// <summary>
/// Pure Dapr HTTP endpoint task invoker - stateless execution with strongly-typed binding.
/// Receives prepared EndpointName, Path, Method and Body.
/// </summary>
public sealed class DaprHttpEndpointTaskInvoker(
    DaprClient daprClient,
    ILogger<DaprHttpEndpointTaskInvoker> logger,
    ITaskMetrics? metrics = null)
    : ITaskInvoker<DaprHttpEndpointBinding>
{
    private readonly ITaskMetrics _metrics = metrics ?? NullTaskMetrics.Instance;

    /// <inheritdoc />
    public string TaskType => TaskTypes.DaprHttpEndpoint;

    /// <inheritdoc />
    public Type BindingType => typeof(DaprHttpEndpointBinding);

    /// <inheritdoc />
    public async Task<TaskInvocationResult> InvokeAsync(
        TaskDescriptor<DaprHttpEndpointBinding> descriptor,
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
        var typedBinding = binding.Deserialize<DaprHttpEndpointBinding>()
            ?? throw new InvalidOperationException("Failed to deserialize DaprHttpEndpointBinding");
        
        return await ExecuteAsync(taskKey, typedBinding, cancellationToken);
    }

    private async Task<TaskInvocationResult> ExecuteAsync(
        string? taskKey,
        DaprHttpEndpointBinding binding,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Parse body if present
            object? body = null;
            if (!string.IsNullOrEmpty(binding.Body))
            {
                body = JsonSerializer.Deserialize<object>(binding.Body);
            }

            var response = await daprClient.InvokeMethodAsync<object?, object>(
                new HttpMethod(binding.Method),
                binding.EndpointName,
                binding.Path,
                body,
                cancellationToken);

            stopwatch.Stop();
            _metrics.RecordDaprServiceInvocation(binding.EndpointName, binding.Path, "success");

            return TaskInvocationResult.Success(
                data: response,
                body: response != null ? JsonSerializer.Serialize(response) : null,
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["EndpointName"] = binding.EndpointName,
                    ["Path"] = binding.Path,
                    ["Method"] = binding.Method
                });
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _metrics.RecordDaprServiceInvocation(binding.EndpointName, binding.Path, "cancelled");
            logger.LogWarning("Dapr HTTP endpoint invocation was cancelled: {EndpointName}/{Path}",
                binding.EndpointName, binding.Path);

            return TaskInvocationResult.Failure(
                error: "Dapr HTTP endpoint invocation was cancelled",
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["EndpointName"] = binding.EndpointName,
                    ["Path"] = binding.Path,
                    ["Cancelled"] = true
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordDaprServiceInvocation(binding.EndpointName, binding.Path, "failure");
            logger.LogError(ex, "Dapr HTTP endpoint invocation failed: {EndpointName}/{Path}",
                binding.EndpointName, binding.Path);

            return TaskInvocationResult.Failure(
                error: ex.Message,
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                taskType: TaskType,
                metadata: new Dictionary<string, object>
                {
                    ["EndpointName"] = binding.EndpointName,
                    ["Path"] = binding.Path,
                    ["ExceptionType"] = ex.GetType().Name,
                    ["StackTrace"] = ex.StackTrace ?? string.Empty
                });
        }
    }
}
