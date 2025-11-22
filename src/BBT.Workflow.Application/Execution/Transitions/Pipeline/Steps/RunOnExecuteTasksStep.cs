using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using BBT.Workflow.Logging;
using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that executes the transition's OnExecute tasks.
/// These tasks run before the state change occurs.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class RunOnExecuteTasksStep(
    ITaskOrchestrationService taskOrchestrationService,
    IScriptContextFactory scriptContextFactory,
    IInstanceRepository instanceRepository) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.OnExecute;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(RunOnExecuteTasksStep)}");

        // Skip if no transition or no OnExecute tasks
        if (context.Transition == null || !context.Transition.OnExecutionTasks.Any())
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }
        
        return await ResultExtensions.TryAsync<StepOutcome>(async ct =>
        {
            // Get or build script context
            var scriptContext = await context.GetOrBuildScriptContextAsync(
                _ => CreateScriptContextAsync(context, ct),
                ct);

            // Get the transition record from previous step
            var instanceTransitionId = context.Items.TryGetValue("TransitionRecordId", out var record) 
                ? record as Guid? 
                : null;

            // Execute the tasks
            await taskOrchestrationService.ExecuteAsync(
                context.Transition.OnExecutionTasks,
                instanceTransitionId,
                TaskTrigger.OnExecute,
                scriptContext,
                ct);

            context.ApplyScriptContextChanges(scriptContext);
            
            await instanceRepository.UpdateAsync(context.Instance, true, ct);
            return StepOutcome.Continue();
        },
        cancellationToken);
    }

    /// <summary>
    /// Creates a script context for task execution.
    /// </summary>
    private async Task<ScriptContext> CreateScriptContextAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithTransition(context.Transition)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
            .BuildAsync(cancellationToken);
    }
}
