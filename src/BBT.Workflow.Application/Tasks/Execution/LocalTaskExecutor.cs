using System.Text;
using System.Text.Json;
using System.Diagnostics;
using BBT.Aether.Guids;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks.Factory;
using BBT.Workflow.Instances;
using BBT.Workflow.Tasks.Persistence;
using BBT.Workflow.Monitoring;

namespace BBT.Workflow.Tasks.Execution;

/// <summary>
/// Local implementation of IWorkflowTaskExecutor that executes tasks directly without remote calls.
/// This is used in the Execution service where tasks are executed locally.
/// </summary>
/// <param name="taskExecutorFactory">Factory for creating task executors based on task type.</param>
/// <param name="guidGenerator">Generator for creating unique identifiers.</param>
/// <param name="taskPersistenceStrategyFactory">Factory for task persistence strategies.</param>
/// <param name="taskFactory">Factory for creating task instances.</param>
/// <param name="workflowMetrics">Service for recording task execution metrics.</param>
public sealed class LocalTaskExecutor(
    ITaskExecutorFactory taskExecutorFactory,
    IGuidGenerator guidGenerator,
    ITaskPersistenceStrategyFactory taskPersistenceStrategyFactory,
    ITaskFactory taskFactory,
    IWorkflowMetrics workflowMetrics) : ITaskOrchestrator
{
    /// <summary>
    /// Executes a task locally with comprehensive error handling and state management.
    /// This is the core implementation extracted from TaskExecutionService.ExecuteSingleTaskAsync.
    /// </summary>
    /// <param name="onExecuteTask">The task configuration to execute.</param>
    /// <param name="instanceTransition">The instance transition context. Can be null for extension tasks.</param>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <param name="context">The script execution context for task execution.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method handles the complete lifecycle of a task execution including:
    /// - Task creation and initialization
    /// - Execution with error handling
    /// - Response processing and context updates
    /// - State transitions (Completed/Faulted)
    /// - Persistence handling via Strategy Pattern
    /// </remarks>
    public async Task ExecuteTaskAsync(
        OnExecuteTask onExecuteTask,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        // Use TaskFactory for optimized task creation with proper isolation
        var task = await taskFactory.CreateExecutionTaskAsync(onExecuteTask.Task, cancellationToken);

        var taskExecutor = taskExecutorFactory.GetExecutor(task.GetTaskType());
        var instanceTask = new InstanceTask(
            guidGenerator.Create(),
            instanceTransition?.Id ?? guidGenerator.Create(),
            task.Key
        );

        // Get the appropriate persistence strategy based on TaskTrigger
        var persistenceStrategy = taskPersistenceStrategyFactory.GetStrategy(taskTrigger);

        // Handle task creation persistence
        await persistenceStrategy.HandleCreationAsync(instanceTask, cancellationToken);

        var taskType = task.GetTaskType().ToString();
        
        // Record task execution start 
        workflowMetrics.RecordTaskExecution(taskType, "started");
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await taskExecutor.ExecuteAsync(
                task,
                onExecuteTask.Mapping.DecodedCode,
                context,
                cancellationToken);

            if (response != null)
            {
                var variableKey = task.Key.ToVariableName();
                context.TaskResponse[variableKey] = response;

                if (taskTrigger != TaskTrigger.Extension)
                {
                    // NoTracking Instance
                    context.Instance.AddData(
                        guidGenerator.Create(),
                        new JsonData(JsonSerializer.Serialize(response, JsonSerializerConstants.JsonOptions)),
                        VersionStrategy.IncreasePatch
                    );
                }
            }

            instanceTask.Completed(
                new JsonData(JsonSerializer.Serialize(response, JsonSerializerConstants.JsonOptions)));
            
            // Record successful task completion 
            stopwatch.Stop();
            workflowMetrics.RecordTaskExecution(taskType, "success");
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            instanceTask.Faulted(e.Message);
            
            // Record task failure
            workflowMetrics.RecordTaskExecution(taskType, "failure");
        }

        // Handle task completion persistence
        await persistenceStrategy.HandleCompletionAsync(instanceTask, cancellationToken);
    }

    /// <summary>
    /// Executes a task locally and returns context updates for synchronization.
    /// This method is specifically designed for distributed scenarios where context changes
    /// need to be synchronized back to the orchestration service.
    /// </summary>
    /// <param name="onExecuteTask">The task configuration to execute.</param>
    /// <param name="instanceTransition">The instance transition context. Can be null for extension tasks.</param>
    /// <param name="taskTrigger">The trigger type that initiated the task execution.</param>
    /// <param name="context">The script execution context for task execution.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>Context updates that occurred during task execution.</returns>
    public async Task<TaskContextUpdateOutput> ExecuteTaskWithContextUpdateAsync(
        OnExecuteTask onExecuteTask,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        // Capture initial state for comparison
        var initialBody = context.Body;
        var initialInstanceDataCount = context.Instance.DataList.Count;
        
        // Execute the task (this already includes metrics recording)
        await ExecuteTaskAsync(onExecuteTask, instanceTransition, taskTrigger, context, cancellationToken);

        // Capture context changes
        var contextUpdate = new TaskContextUpdateOutput
        {
            TaskResponse = new Dictionary<string, object?>(context.TaskResponse)
        };

        // Capture instance data changes with proper thread safety
        if (context.Instance.DataList.Count > initialInstanceDataCount)
        {
            // Get the new data entries that were added during execution
            var newDataEntries = context.Instance.DataList
                .Skip(initialInstanceDataCount)
                .ToDictionary(
                    d => d.Id.ToString(), // Use version as key for proper synchronization
                    d => new TaskInstanceDataUpdatesOutput
                    {
                        Id = d.Id,
                        Data = d.Data.Json
                    });
            contextUpdate.InstanceDataUpdates = newDataEntries;
        }

        // Check if body was modified
        var bodyModified = !ReferenceEquals(initialBody, context.Body) ||
                           (initialBody != null && context.Body != null && !initialBody.Equals(context.Body));

        if (bodyModified)
        {
            contextUpdate.Body = context.Body;
            contextUpdate.BodyModified = true;
        }

        // Copy metadata (if present)
        if (context.MetaData.Any())
        {
            contextUpdate.MetaData = new Dictionary<string, object>(context.MetaData);
        }

        return contextUpdate;
    }
}