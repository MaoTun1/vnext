using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that executes the current state's OnExit tasks.
/// These tasks run when leaving the current state.
/// </summary>
public sealed class RunOnExitTasksStep(
    ITaskOrchestrationService taskOrchestrationService,
    IScriptContextFactory scriptContextFactory,
    ILogger<RunOnExitTasksStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.OnExit;

    /// <inheritdoc />
    public async Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Current.OnExits.Any())
        {
            logger.LogTrace("No OnExit tasks for state {StateName}", context.Current.Key);
            return;
        }

        logger.LogDebug("Executing OnExit tasks for state {StateName} on instance {InstanceId}",
            context.Current.Key, context.InstanceId);

        // Get or build script context
        var scriptContext = context.GetOrBuildScriptContext(() => 
            CreateScriptContext(context));

        // Get the transition record from previous step
        var instanceTransitionId = context.Items.TryGetValue("TransitionRecordId", out var record) 
            ? record as Guid? 
            : null;

        // Execute the tasks
        await taskOrchestrationService.ExecuteAsync(
            context.Current.OnExits,
            instanceTransitionId,
            TaskTrigger.OnExit,
            scriptContext,
            cancellationToken);

        logger.LogDebug("Completed OnExit tasks for state {StateName}", context.Current.Key);
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
