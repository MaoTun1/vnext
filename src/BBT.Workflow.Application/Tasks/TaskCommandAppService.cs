using BBT.Workflow.Definitions;
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
    /// <returns>Context updates that occurred during task execution.</returns>
    public async Task<TaskContextUpdateOutput> ExecuteTaskAsync(
        TaskExecutionRequestInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Context.Workflow.Domain);
        using (currentSchema.Change(input.Context.Workflow.Key))
        {
            var onExecuteTask = OnExecuteTask.Create(
                input.OnExecuteTask.Order,
                input.OnExecuteTask.Task,
                new ScriptCode(input.OnExecuteTask.Mapping.Location, input.OnExecuteTask.Mapping.Code)
            );

            InstanceTransition? instanceTransition = null;

            if (input.InstanceTransitionId.HasValue)
            {
                instanceTransition =
                    await instanceTransitionRepository.FindAsync(input.InstanceTransitionId.Value, true,
                        cancellationToken);
            }

            // Use the ScriptContextFactory to create the context with all necessary mappings
            var scriptContext = await scriptContextFactory.CreateFromTaskRequestAsync(
                input,
                runtimeInfoProvider, 
                cancellationToken);

            // Use LocalTaskExecutor for context tracking
            if (taskOrchestrator is LocalTaskExecutor localExecutor)
            {
                return await localExecutor.ExecuteTaskWithContextUpdateAsync(
                    onExecuteTask,
                    instanceTransition,
                    input.TaskTrigger,
                    scriptContext,
                    cancellationToken);
            }
            
            // Return empty context update as fallback
            return new TaskContextUpdateOutput();
        }
    }
}