using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling Trigger (Direct) trigger type.
/// Executes a transition on the current workflow instance using HttpTask to call the transition endpoint.
/// </summary>
public sealed class DirectTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ITaskExecutorFactory _taskExecutorFactory;
    private readonly ITriggerTransitionHttpTaskFactory _httpTaskFactory;
    private readonly ILogger<DirectTriggerStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectTriggerStrategy"/> class.
    /// </summary>
    /// <param name="taskExecutorFactory">Factory for creating task executors.</param>
    /// <param name="httpTaskFactory">Factory for creating HTTP tasks for trigger transitions.</param>
    /// <param name="logger">The logger instance.</param>
    public DirectTriggerStrategy(
        ITaskExecutorFactory taskExecutorFactory,
        ITriggerTransitionHttpTaskFactory httpTaskFactory,
        ILogger<DirectTriggerStrategy> logger)
    {
        _taskExecutorFactory = taskExecutorFactory;
        _httpTaskFactory = httpTaskFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.TransitionName))
            throw new InvalidOperationException("TransitionName is required for Trigger (Direct) trigger type");

        _logger.LogInformation("Handling Direct trigger for task {TaskKey} - InstanceId: {InstanceId}, Transition: {Transition}",
            task.Key, context.Instance.Id, task.TransitionName);

        // Resolve instance ID
        var instanceId = await GetInstanceIdAsync(task, context, cancellationToken);

        // Create path using InstanceUrlTemplates.Transition format
        var path = string.Format(InstanceUrlTemplates.Transition,
            task.TriggerDomain,
            task.TriggerFlow,
            instanceId,
            task.TransitionName);

        var httpTask = _httpTaskFactory.CreateHttpTask(task, context, path, "PATCH");

        var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");

        _logger.LogDebug("Calling HttpTaskExecutor.CallAsync for Direct trigger task {TaskKey}", task.Key);
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }

    /// <summary>
    /// Resolves the instance ID for the transition based on task configuration.
    /// </summary>
    /// <param name="task">The trigger transition task.</param>
    /// <param name="context">The script context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved instance ID.</returns>
    private async Task<string> GetInstanceIdAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        // If TriggerInstanceId is provided, use it
        if (!string.IsNullOrWhiteSpace(task.TriggerInstanceId))
        {
            _logger.LogDebug("Using provided TriggerInstanceId: {InstanceId}", task.TriggerInstanceId);
            return task.TriggerInstanceId;
        }

        // If TriggerKey is provided but TriggerInstanceId is not, query the instance
        if (!string.IsNullOrWhiteSpace(task.TriggerKey))
        {
            _logger.LogInformation("TriggerInstanceId not provided but TriggerKey exists: {Key}. Querying instance by key.", 
                task.TriggerKey);

            var path = string.Format(InstanceUrlTemplates.Instance,
                task.TriggerDomain,
                task.TriggerFlow,
                task.TriggerKey);

            var httpTask = _httpTaskFactory.CreateHttpTask(task, context, path, "GET");

            var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;
            if (httpExecutor == null)
                throw new InvalidOperationException("HttpTaskExecutor not found");

            _logger.LogDebug("Calling HttpTaskExecutor.CallAsync to get instance by key {Key}", task.TriggerKey);
            
            // Store original body to restore after the call
            object? originalBody = context.Body;
            
            await httpExecutor.CallAsync(httpTask, context, cancellationToken);

            // Parse the response and extract the Id
            // After CallAsync, context.Body contains StandardTaskResponse merged data
            // The actual response data is in context.Body.data property (camelCase)
            if (context.Body != null)
            {
                try
                {
                    // Access the 'data' property from StandardTaskResponse
                    dynamic bodyData = context.Body;
                    var responseData = bodyData.data;
                    
                    if (responseData != null)
                    {
                        var jsonElement = JsonSerializer.SerializeToElement(responseData);
                        
                        // Use case-insensitive deserialization to match camelCase JSON properties to PascalCase C# properties
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var instanceOutput = JsonSerializer.Deserialize<GetInstanceOutput>(jsonElement, options);

                        if (instanceOutput != null)
                        {

                            string idValue = instanceOutput.Id?.ToString() ?? "null";
                            // Use GetValueOrDefault() to safely access Nullable<Guid>
                            Guid resolvedGuid = Guid.TryParse(idValue, out var instanceId)?instanceId:Guid.Empty;
                            
                            if (resolvedGuid != Guid.Empty)
                            {
                                string resolvedId = resolvedGuid.ToString();
                                string? TriggerKey = task.TriggerKey;
                                _logger.LogInformation("Resolved instance ID from key {Key}: {InstanceId}", 
                                    TriggerKey, resolvedId);
                                
                                // Restore original body
                                context.SetBody(originalBody);
                                
                                return resolvedId;
                            }
                        }

                        throw new InvalidOperationException($"Instance with key '{task.TriggerKey}' returned no valid Id");
                    }

                    throw new InvalidOperationException($"Response data is null when querying instance by key '{task.TriggerKey}'");
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize GetInstanceOutput for key {Key}", task.TriggerKey);
                    throw new InvalidOperationException($"Failed to parse instance response for key '{task.TriggerKey}'", ex);
                }
                catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                {
                    _logger.LogError(ex, "Failed to access response data property for key {Key}", task.TriggerKey);
                    throw new InvalidOperationException($"Invalid response structure when querying instance by key '{task.TriggerKey}'", ex);
                }
            }

            throw new InvalidOperationException($"No response received when querying instance by key '{task.TriggerKey}'");
        }

        // Default to current instance ID
        _logger.LogDebug("Using current instance ID: {InstanceId}", context.Instance.Id);
        return context.Instance.Id.ToString();
    }
}

