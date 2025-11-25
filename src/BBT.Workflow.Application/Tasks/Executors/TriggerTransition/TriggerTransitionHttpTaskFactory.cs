using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Factory implementation for creating HTTP tasks used in trigger transition strategies.
/// Also provides utilities for resolving instance IDs.
/// </summary>
public sealed class TriggerTransitionHttpTaskFactory : ITriggerTransitionHttpTaskFactory
{
    private readonly IConfiguration _configuration;
    private readonly ITaskExecutorFactory _taskExecutorFactory;
    private readonly ILogger<TriggerTransitionHttpTaskFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerTransitionHttpTaskFactory"/> class.
    /// </summary>
    /// <param name="configuration">The configuration instance for reading vNextApi settings.</param>
    /// <param name="taskExecutorFactory">Factory for creating task executors.</param>
    /// <param name="logger">The logger instance.</param>
    public TriggerTransitionHttpTaskFactory(
        IConfiguration configuration,
        ITaskExecutorFactory taskExecutorFactory,
        ILogger<TriggerTransitionHttpTaskFactory> logger)
    {
        _configuration = configuration;
        _taskExecutorFactory = taskExecutorFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Result<HttpTask> CreateHttpTask(
        WorkflowTask task,
        ScriptContext context,
        string path,
        string method)
    {
        return ResultExtensions.Try(() =>
        {
            // Build full URL using configuration
            var baseUrl = _configuration["vNextApi:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            var apiVersion = _configuration["vNextApi:ApiVersion"] ?? "1";
            var fullUrl = $"{baseUrl}/api/v{apiVersion}{path}";

            _logger.LogDebug("Creating HttpTask with URL: {Url}", fullUrl);

            // Prepare body from task.Body or context.Body
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
            var timeoutSeconds = _configuration.GetValue<int>("vNextApi:TimeoutSeconds", 30);

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

            return httpTask;
        },
        ex => Error.Failure(WorkflowErrorCodes.TriggerCreateHttpTaskFailed, $"Failed to create HTTP task: {ex.Message}"));
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
            _logger.LogDebug("Using provided TriggerInstanceId: {InstanceId}", instanceId);
            return Result<string>.Ok(instanceId);
        }

        // Priority 2: If TriggerKey is provided but TriggerInstanceId is not, query the instance
        if (!string.IsNullOrWhiteSpace(key))
        {
            _logger.LogInformation("TriggerInstanceId not provided but TriggerKey exists: {Key}. Querying instance by key.",
                key);
            // For GetInstanceDataTask, use the key directly
            if (task is GetInstanceDataTask)
            {
                return Result<string>.Ok(key);
            }
            return await ResolveInstanceIdByKeyAsync(task, key, domain, flow, context, cancellationToken);
        }

        // Priority 3: Default to current instance ID
        _logger.LogDebug("Using current instance ID: {InstanceId}", context.Instance.Id);
        return Result<string>.Ok(context.Instance.Id.ToString());
    }

    /// <summary>
    /// Extracts instance-related properties from task.
    /// </summary>
    private static (string? instanceId, string? key, string domain, string flow) ExtractInstanceProperties(WorkflowTask task)
    {
        return task switch
        {
            DirectTriggerTask directTask => (directTask.TriggerInstanceId, directTask.TriggerKey, directTask.TriggerDomain, directTask.TriggerFlow),
            GetInstanceDataTask getDataTask => (getDataTask.TriggerInstanceId, getDataTask.TriggerKey, getDataTask.TriggerDomain, getDataTask.TriggerFlow),
            _ => (null, null, string.Empty, string.Empty)
        };
    }

    /// <summary>
    /// Resolves the instance ID by querying the instance using the TriggerKey.
    /// </summary>
    /// <param name="task">The trigger transition task.</param>
    /// <param name="key">The instance key to resolve.</param>
    /// <param name="domain">The workflow domain.</param>
    /// <param name="flow">The workflow flow name.</param>
    /// <param name="context">The script context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the resolved instance ID or an error.</returns>
    private async Task<Result<string>> ResolveInstanceIdByKeyAsync(
        WorkflowTask task,
        string key,
        string domain,
        string flow,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                var path = string.Format(InstanceUrlTemplates.Instance,
                    domain,
                    flow,
                    key);

                var httpTaskResult = CreateHttpTask(task, context, path, "GET");
                if (!httpTaskResult.IsSuccess)
                {
                    _logger.LogError("Failed to create HTTP task for key resolution: {Error}", httpTaskResult.Error.Code);
                    throw new InvalidOperationException($"Failed to create HTTP task: {httpTaskResult.Error.Message}");
                }

                var httpTask = httpTaskResult.Value!;

                var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;
                if (httpExecutor == null)
                    throw new InvalidOperationException("HttpTaskExecutor not found");

                _logger.LogDebug("Calling HttpTaskExecutor.CallAsync to get instance by key {Key}", key);

                // Store original body to restore after the call
                object? originalBody = context.Body;

                await httpExecutor.CallAsync(httpTask, context, ct);

                // Parse the response and extract the Id
                // After CallAsync, context.Body contains StandardTaskResponse merged data
                // The actual response data is in context.Body.data property (camelCase)
                if (context.Body == null)
                {
                    throw new InvalidOperationException($"No response received when querying instance by key '{key}'");
                }

                var instanceIdResult = ExtractInstanceIdFromResponse(context.Body, key);
                if (!instanceIdResult.IsSuccess)
                {
                    throw new InvalidOperationException(instanceIdResult.Error.Message ?? "Failed to extract instance ID");
                }

                string instanceId = instanceIdResult.Value!;

                // Restore original body
                context.SetBody(originalBody);

                _logger.LogInformation("Resolved instance ID from key {Key}: {InstanceId}",
                    key, instanceId);

                return instanceId;
            },
            cancellationToken,
            ex => Error.Dependency(WorkflowErrorCodes.TriggerResolveInstanceFailed, 
                $"Failed to resolve instance ID for key '{key}': {ex.Message}"));
    }

    /// <summary>
    /// Extracts the instance ID from the HTTP response body.
    /// </summary>
    /// <param name="responseBody">The response body containing the instance data.</param>
    /// <param name="triggerKey">The trigger key used for error messages.</param>
    /// <returns>A Result containing the extracted instance ID or an error.</returns>
    private Result<string> ExtractInstanceIdFromResponse(object responseBody, string triggerKey)
    {
        return ResultExtensions.Try(() =>
        {
            // Access the 'data' property from StandardTaskResponse
            dynamic bodyData = responseBody;
            var responseData = bodyData.data;

            if (responseData == null)
            {
                throw new InvalidOperationException($"Response data is null when querying instance by key '{triggerKey}'");
            }

            var jsonElement = JsonSerializer.SerializeToElement(responseData);

            // Use case-insensitive deserialization to match camelCase JSON properties to PascalCase C# properties
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var instanceOutput = JsonSerializer.Deserialize<GetInstanceOutput>(jsonElement, options);

            if (instanceOutput == null)
            {
                throw new InvalidOperationException($"Failed to deserialize instance response for key '{triggerKey}'");
            }

            string idValue = instanceOutput.Id?.ToString() ?? "null";
            Guid resolvedGuid = Guid.TryParse(idValue, out var instanceId) ? instanceId : Guid.Empty;

            if (resolvedGuid == Guid.Empty)
            {
                throw new InvalidOperationException($"Instance with key '{triggerKey}' returned no valid Id");
            }

            return resolvedGuid.ToString();
        },
        ex => ex switch
        {
            JsonException jsonEx => Error.Validation(WorkflowErrorCodes.TriggerInvalidResponseFormat, 
                $"Failed to parse instance response for key '{triggerKey}': {jsonEx.Message}"),
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException binderEx => Error.Validation(WorkflowErrorCodes.TriggerInvalidResponseStructure,
                $"Invalid response structure when querying instance by key '{triggerKey}': {binderEx.Message}"),
            _ => Error.Failure(WorkflowErrorCodes.TriggerExtractInstanceIdFailed, 
                $"Failed to extract instance ID for key '{triggerKey}': {ex.Message}")
        });
    }
}

