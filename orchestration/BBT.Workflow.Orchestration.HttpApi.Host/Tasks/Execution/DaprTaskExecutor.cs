using System.Collections.Concurrent;
using System.Diagnostics;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Shared;
using BBT.Workflow.Monitoring;
using Dapr.Client;

namespace BBT.Workflow.Tasks.Execution;

/// <summary>
/// Dapr-based implementation of IWorkflowTaskExecutor that delegates task execution to the Execution service
/// via Dapr Service Invocation. This enables distributed task execution while maintaining
/// the same interface as local execution.
/// </summary>
/// <param name="daprClient">The Dapr client for service invocation calls.</param>
/// <param name="logger">Logger for recording service invocation activities.</param>
/// <param name="configuration">Configuration for service settings.</param>
/// <param name="workflowMetrics">Service for recording task execution metrics.</param>
public sealed class DaprTaskExecutor(
    DaprClient daprClient,
    ILogger<DaprTaskExecutor> logger,
    IConfiguration configuration,
    IWorkflowMetrics workflowMetrics) : ITaskOrchestrator
{
    private readonly string _executionServiceAppId = configuration["ExecutionService:AppId"] ?? "vnext-execution";
    private readonly ConcurrentDictionary<string, object> _contextLocks = new();

    /// <summary>
    /// Executes a task by delegating to the Execution service via Dapr Service Invocation.
    /// Automatically synchronizes context changes from the remote execution back to the local context.
    /// </summary>
    /// <param name="onExecuteTask">The task configuration to execute.</param>
    /// <param name="instanceTransition">The instance transition context. Can be null for extension tasks.</param>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <param name="context">The script execution context for task execution.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Execution service call fails.</exception>
    public async Task ExecuteTaskAsync(
        OnExecuteTask onExecuteTask,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onExecuteTask);
        ArgumentNullException.ThrowIfNull(context);

        logger.LogInformation("Delegating execution of task {TaskKey} for instance {InstanceId} to Execution service",
            onExecuteTask.Task.Key, context.Instance.Id);

        // Convert Domain models to DTOs
        var input = MapToRequest(
            onExecuteTask,
            instanceTransition,
            taskTrigger,
            context);

        // Extract task info for metrics
        var taskType = input.OnExecuteTask.Task.Key; // Task key can represent task type
        var workflowKey = context.Workflow.Key;

        // Record task execution start and update gauges (remote execution)
        workflowMetrics.RecordTaskExecuted(taskType, workflowKey);
        workflowMetrics.StartTaskExecution(taskType, workflowKey); // pending--, running++

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await daprClient.InvokeMethodAsync<TaskExecutionRequestInput, TaskExecutionResponseOutput>(
                HttpMethod.Post,
                _executionServiceAppId,
                "/api/v1/execution/task",
                input,
                cancellationToken);

            stopwatch.Stop();

            if (!response.Success)
            {
                // Record task failure (remote execution failed)
                workflowMetrics.RecordTaskFailed(taskType, workflowKey, stopwatch.Elapsed.TotalSeconds);
                workflowMetrics.FinishTaskExecution(taskType, workflowKey); // running--
                throw new InvalidOperationException($"Task execution failed: {response.Message}");
            }

            // Record successful task completion (remote execution succeeded)
            workflowMetrics.RecordTaskCompleted(taskType, workflowKey, stopwatch.Elapsed.TotalSeconds);
            workflowMetrics.FinishTaskExecution(taskType, workflowKey); // running--

            // Apply context updates from remote execution to local context
            if (response.ContextUpdate != null)
            {
                await ApplyContextUpdatesAsync(context, response.ContextUpdate, cancellationToken);

                logger.LogInformation(
                    "Applied context updates for task {TaskKey}: TaskResponse={TaskResponseCount}, InstanceData={InstanceDataCount}, BodyModified={BodyModified}",
                    onExecuteTask.Task.Key,
                    response.ContextUpdate.TaskResponse.Count,
                    response.ContextUpdate.InstanceDataUpdates.Count,
                    response.ContextUpdate.BodyModified);
            }

            logger.LogInformation("Successfully executed task {TaskKey} for instance {InstanceId}",
                onExecuteTask.Task.Key, context.Instance.Id);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            stopwatch.Stop();

            // Record task failure (communication error with execution service)
            workflowMetrics.RecordTaskFailed(taskType, workflowKey, stopwatch.Elapsed.TotalSeconds);
            workflowMetrics.FinishTaskExecution(taskType, workflowKey); // running--

            logger.LogError(ex, "Error calling Execution service for task {TaskKey} for instance {InstanceId}",
                onExecuteTask.Task.Key, context.Instance.Id);
            throw new InvalidOperationException($"Failed to execute task via Execution service: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies context updates received from the Execution service to the local context.
    /// This method handles the synchronization of context changes in a thread-safe manner.
    /// Uses context-based locking to allow parallel updates for different contexts while 
    /// ensuring thread safety for the same context instance.
    /// </summary>
    /// <param name="context">The local script context to update.</param>
    /// <param name="contextUpdate">The context updates received from the Execution service.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private Task ApplyContextUpdatesAsync(ScriptContext context, TaskContextUpdateOutput contextUpdate,
        CancellationToken cancellationToken = default)
    {
        // Get or create a lock object specific to this context instance
        var contextLockKey = $"{context.Instance.Id}_{context.Workflow.Key}";
        var contextLock = _contextLocks.GetOrAdd(contextLockKey, _ => new object());

        // Use context-specific lock to allow parallel updates for different contexts
        // but serialize updates for the same context instance
        lock (contextLock)
        {
            // Apply TaskResponse updates
            if (contextUpdate.TaskResponse.Any())
            {
                foreach (var kvp in contextUpdate.TaskResponse)
                {
                    context.TaskResponse[kvp.Key] = kvp.Value;
                }
            }

            // Apply instance data updates (Critical for data consistency)
            if (contextUpdate.InstanceDataUpdates?.Any() == true)
            {
                foreach (var kvp in contextUpdate.InstanceDataUpdates)
                {
                    var dataInfo = kvp.Value;

                    if (dataInfo != null && dataInfo.Data.IsNullOrEmpty())
                    {
                        // Add the new data entry with the correct version
                        context.Instance.AddData(
                            dataInfo.Id,
                            new JsonData(dataInfo.Data),
                            VersionStrategy.IncreasePatch
                        );
                    }
                }
            }

            // Apply body updates if modified
            if (contextUpdate.BodyModified && contextUpdate.Body != null)
            {
                context.SetBody(contextUpdate.Body);
            }

            // Apply metadata updates
            if (contextUpdate.MetaData?.Any() == true)
            {
                foreach (var kvp in contextUpdate.MetaData)
                {
                    context.MetaData[kvp.Key] = kvp.Value;
                }
            }
        }

        // Clean up lock objects for completed instances to prevent memory leaks
        // This can be done periodically or when instance is completed
        _contextLocks.TryRemove(contextLockKey, out _);

        return Task.CompletedTask;
    }

    private TaskExecutionRequestInput MapToRequest(
        OnExecuteTask onExecuteTask,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context)
    {
        return new TaskExecutionRequestInput
        {
            OnExecuteTask = new OnExecuteTaskInput()
            {
                Order = onExecuteTask.Order,
                Task = new ReferenceInput()
                {
                    Domain = onExecuteTask.Task.Domain,
                    Flow = onExecuteTask.Task.Flow,
                    Key = onExecuteTask.Task.Key,
                    Version = onExecuteTask.Task.Version
                },
                Mapping = new ScriptCodeInput()
                {
                    Location = onExecuteTask.Mapping.Location,
                    Code = onExecuteTask.Mapping.Code
                }
            },
            InstanceTransitionId = instanceTransition?.Id,
            TaskTrigger = taskTrigger,
            Context = new TaskScriptContextModel()
            {
                InstanceId = context.Instance.Id,
                TransitionKey = context.Transition?.Key,
                Workflow = new ReferenceInput()
                {
                    Domain = context.Workflow.Domain,
                    Flow = context.Workflow.Flow,
                    Key = context.Workflow.Key,
                    Version = context.Workflow.Version
                },
                Body = context.Body,
                Headers = context.Headers,
                RouteValues = context.RouteValues,
                TaskResponse = context.TaskResponse,
                MetaData = context.MetaData,
                Definitions = context.Definitions
            }
        };
    }
}