using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that executes the transition's OnExecute tasks.
/// These tasks run before the state change occurs.
/// </summary>
public sealed class RunOnExecuteTasksStep(
    ITaskOrchestrationService taskOrchestrationService,
    IScriptContextFactory scriptContextFactory,
    IInstanceRepository instanceRepository,
    ILogger<RunOnExecuteTasksStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.OnExecute;

    /// <inheritdoc />
    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Transition.OnExecutionTasks.Any())
        {
            logger.LogTrace("No OnExecute tasks for transition {TransitionKey}", context.TransitionKey);
            return StepOutcome.Continue();
        }

        logger.LogDebug("Executing OnExecute tasks for transition {TransitionKey} on instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        // Get or build script context
        var scriptContext = context.GetOrBuildScriptContext(() => 
            CreateScriptContext(context));

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
            cancellationToken);

        context.ApplyScriptContextChanges(scriptContext);
        
        await instanceRepository.UpdateAsync(context.Instance, true, cancellationToken);

        logger.LogDebug("Completed OnExecute tasks for transition {TransitionKey}", context.TransitionKey);
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
