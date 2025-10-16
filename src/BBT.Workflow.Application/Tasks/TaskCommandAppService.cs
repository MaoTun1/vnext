using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks.Extensions;
using BBT.Workflow.Tasks.Execution;

namespace BBT.Workflow.Tasks;

public sealed class TaskCommandAppService(
    IRuntimeInfoProvider runtimeInfoProvider,
    ICurrentSchema currentSchema,
    IScriptContextFactory scriptContextFactory,
    IInstanceTransitionRepository instanceTransitionRepository,
    ITaskOrchestrator taskOrchestrator
) : ITaskCommandAppService
{
    /// <summary>
    /// Executes a task and returns context updates for distributed synchronization.
    /// This method is used when the execution service needs to return context changes
    /// back to the orchestration service.
    /// </summary>
    /// <param name="input">The task execution request input.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A Result containing context updates that occurred during task execution.</returns>
    public async Task<Result<TaskContextUpdateOutput>> ExecuteTaskAsync(
        TaskExecutionRequestInput input,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate domain
        var domainCheck = runtimeInfoProvider.Check(input.Context.Workflow.Domain);
        if (!domainCheck.IsSuccess)
            return Result<TaskContextUpdateOutput>.Fail(domainCheck.Error);

        using (currentSchema.Change(input.Context.Workflow.Key))
        {
            // 2. Create task execution configuration
            var onExecuteTask = OnExecuteTask.Create(
                input.OnExecuteTask.Order,
                input.OnExecuteTask.Task,
                new ScriptCode(input.OnExecuteTask.Mapping.Location, input.OnExecuteTask.Mapping.Code)
            );

            // 3. Create script context with all necessary mappings
            var scriptContextResult = await ResultExtensions.TryAsync(
                async ct => await scriptContextFactory.CreateFromTaskRequestAsync(
                    input,
                    runtimeInfoProvider, 
                    ct),
                cancellationToken,
                ex => Error.Failure("task.contextCreation", $"Failed to create script context: {ex.Message}"));

            if (!scriptContextResult.IsSuccess)
                return Result<TaskContextUpdateOutput>.Fail(scriptContextResult.Error);

            var scriptContext = scriptContextResult.Value!;

            // 4. Execute task with context tracking
            if (taskOrchestrator is LocalTaskExecutor localExecutor)
            {
                var executionResult = await ResultExtensions.TryAsync(
                    async ct => await localExecutor.ExecuteTaskWithContextUpdateAsync(
                        onExecuteTask,
                        input.InstanceTransitionId,
                        input.TaskTrigger,
                        scriptContext,
                        ct),
                    cancellationToken,
                    ex => Error.Failure("task.execution", $"Task execution failed: {ex.Message}"));

                return executionResult;
            }
            
            // 5. Return empty context update as fallback
            return Result<TaskContextUpdateOutput>.Ok(new TaskContextUpdateOutput());
        }
    }
}