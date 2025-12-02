using System.Text.Json;
using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Configuration;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Factory implementation for creating HTTP tasks used in trigger transition strategies.
/// Also provides utilities for resolving instance IDs.
/// </summary>
public sealed class TriggerTransitionHttpTaskFactory(IConfiguration configuration) : ITriggerTransitionHttpTaskFactory
{
    /// <inheritdoc />
    public Result<HttpTask> CreateHttpTask(
        WorkflowTask task,
        ScriptContext context,
        string path,
        string method)
    {
        // Build full URL using configuration
        var baseUrl = configuration["vNextApi:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
        var apiVersion = configuration["vNextApi:ApiVersion"] ?? "1";
        var fullUrl = $"{baseUrl}/api/v{apiVersion}{path}";

        // Prepare body from triggerTask.Body or context.Body
        JsonElement? body = GetBodyFromTask(task);
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
        var timeoutSeconds = configuration.GetValue("vNextApi:TimeoutSeconds", 30);

        // Create task config JSON
        var configBuilder = new Dictionary<string, object>
        {
            ["key"] = task.Key,
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

        // Copy base properties from task
        task.CopyBaseToInternal(httpTask);

        return Result<HttpTask>.Ok(httpTask);
    }

    /// <summary>
    /// Extracts body from task if it has a Body property.
    /// </summary>
    private static JsonElement? GetBodyFromTask(WorkflowTask task)
    {
        return task switch
        {
            StartTask startTask => startTask.Body,
            DirectTriggerTask directTask => directTask.Body,
            SubProcessTask subProcessTask => subProcessTask.Body,
            _ => null
        };
    }

    /// <inheritdoc />
    public async Task<Result<string>> ResolveInstanceIdAsync(
        WorkflowTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        // Extract instance ID and key from task
        var (instanceId, key, domain, flow) = ExtractInstanceProperties(task);

        // Priority 1: If TriggerInstanceId is provided, use it
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            return Result<string>.Ok(instanceId);
        }

        // Priority 2: If TriggerKey is provided but TriggerInstanceId is not, query the instance
        if (!string.IsNullOrWhiteSpace(key))
        {
            // For GetInstanceDataTask, use the key directly
            return Result<string>.Ok(key);
        }

        // Priority 3: Default to current instance ID
        return Result<string>.Ok(context.Instance.Id.ToString());
    }

    /// <summary>
    /// Extracts instance-related properties from task.
    /// </summary>
    private static (string? instanceId, string? key, string domain, string flow) ExtractInstanceProperties(
        WorkflowTask task)
    {
        return task switch
        {
            DirectTriggerTask directTask => (directTask.TriggerInstanceId, directTask.TriggerKey,
                directTask.TriggerDomain, directTask.TriggerFlow),
            GetInstanceDataTask getDataTask => (getDataTask.TriggerInstanceId, getDataTask.TriggerKey,
                getDataTask.TriggerDomain, getDataTask.TriggerFlow),
            _ => (null, null, string.Empty, string.Empty)
        };
    }
}