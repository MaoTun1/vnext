using System.Diagnostics;
using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Logging;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks.Extensions;
using BBT.Workflow.Tasks.Execution;
using StackExchange.Redis;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Application service for task command operations using Railway Oriented Programming pattern.
/// Handles task execution with proper error handling and context synchronization.
/// </summary>
public sealed class TaskCommandAppService(
    IRuntimeInfoProvider runtimeInfoProvider,
    IScriptContextFactory scriptContextFactory,
    ITaskOrchestrator taskOrchestrator
) : ITaskCommandAppService
{
    /// <summary>
    /// Executes a task and returns context updates for distributed synchronization.
    /// This method uses Railway pattern for clean error flow without Try blocks,
    /// following the principle that domain/application logic should not use Try.
    /// </summary>
    /// <param name="input">The task execution request input.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A Result containing context updates that occurred during task execution.</returns>
    public async Task<Result<TaskContextUpdateOutput>> ExecuteTaskAsync(
        TaskExecutionRequestInput input,
        CancellationToken cancellationToken = default)
    {
        return await Result.Ok(input)
            .Tap(i => runtimeInfoProvider.Check(i.Context.Workflow.Domain))
            .Bind(CreateOnExecuteTask)
            .BindAsync(onExecuteTask => CreateExecutionContextAsync(input, onExecuteTask, cancellationToken))
            .BindAsync(ctx => ExecuteWithOrchestratorAsync(ctx, cancellationToken));
    }

    /// <summary>
    /// Creates the OnExecuteTask configuration from the input.
    /// </summary>
    private static Result<OnExecuteTask> CreateOnExecuteTask(TaskExecutionRequestInput input)
    {
        var onExecuteTask = OnExecuteTask.Create(
            input.OnExecuteTask.Order,
            input.OnExecuteTask.Task,
            new ScriptCode(
                input.OnExecuteTask.Mapping.Location ?? "./",
                input.OnExecuteTask.Mapping.Code ?? string.Empty,
                input.OnExecuteTask.Mapping.Type)
        );

        return Result<OnExecuteTask>.Ok(onExecuteTask);
    }

    /// <summary>
    /// Creates the task execution context including script context.
    /// </summary>
    private async Task<Result<TaskExecutionContext>> CreateExecutionContextAsync(
        TaskExecutionRequestInput input,
        OnExecuteTask onExecuteTask,
        CancellationToken cancellationToken)
    {
        var scriptContext = await scriptContextFactory.CreateFromTaskRequestAsync(
            input,
            runtimeInfoProvider,
            cancellationToken);

        return Result<TaskExecutionContext>.Ok(
            new TaskExecutionContext(input, onExecuteTask, scriptContext));
    }

    /// <summary>
    /// Executes the task using the appropriate orchestrator.
    /// Returns empty context update if orchestrator is not LocalTaskExecutor.
    /// </summary>
    private async Task<Result<TaskContextUpdateOutput>> ExecuteWithOrchestratorAsync(
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (taskOrchestrator is not LocalTaskExecutor localExecutor)
        {
            return Result<TaskContextUpdateOutput>.Ok(new TaskContextUpdateOutput());
        }
        
        var output = await localExecutor.ExecuteTaskWithContextUpdateAsync(
            context.OnExecuteTask,
            context.Input.InstanceTransitionId,
            context.Input.TaskTrigger,
            context.ScriptContext,
            cancellationToken);

        return Result<TaskContextUpdateOutput>.Ok(output);
    }
}