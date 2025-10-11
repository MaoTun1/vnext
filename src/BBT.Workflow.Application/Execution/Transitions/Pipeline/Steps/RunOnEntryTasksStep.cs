using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that executes the target state's OnEntry tasks.
/// These tasks run when entering the new state.
/// </summary>
public sealed class RunOnEntryTasksStep(
    ITaskOrchestrationService taskOrchestrationService,
    IScriptContextFactory scriptContextFactory,
    IInstanceRepository instanceRepository,
    ILogger<RunOnEntryTasksStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.OnEntry;

    /// <inheritdoc />
    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target?.OnEntries == null || !context.Target.OnEntries.Any())
        {
            logger.LogTrace("No OnEntry tasks for state {StateName}", context.Target?.Key ?? "Unknown");
            return StepOutcome.Continue();
        }

        logger.LogDebug("Executing OnEntry tasks for state {StateName} on instance {InstanceId}",
            context.Target.Key, context.InstanceId);

        // Get or build script context
        var scriptContext = context.GetOrBuildScriptContext(() => 
            CreateScriptContext(context));

        // Get the transition record from previous step
        var instanceTransitionId = context.Items.TryGetValue("TransitionRecordId", out var record) 
            ? record as Guid?
            : null;

        // Execute the tasks
        await taskOrchestrationService.ExecuteAsync(
            context.Target.OnEntries,
            instanceTransitionId,
            TaskTrigger.OnEntry,
            scriptContext,
            cancellationToken);

        context.ApplyScriptContextChanges(scriptContext);

        await instanceRepository.UpdateAsync(context.Instance, true, cancellationToken);
        logger.LogDebug("Completed OnEntry tasks for state {StateName}", context.Target.Key);
        return StepOutcome.Continue();
    }

    /// <summary>
    /// Creates a script context for task execution.
    /// </summary>
    private ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        return scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithTransition(context.Transition)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value))
            .BuildAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
