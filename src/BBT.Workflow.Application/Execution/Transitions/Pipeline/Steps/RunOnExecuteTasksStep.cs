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

        // Skip if no OnExecute tasks
        if (!HasOnExecuteTasks(context))
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway chain: Build context -> Execute tasks -> Apply changes -> Persist
        return await Result.Ok(context)
            .MapAsync(ctx => BuildScriptContextAsync(ctx, cancellationToken))
            .TapAsync(scriptContext => ExecuteTasksAsync(context, scriptContext, cancellationToken))
            .Tap(context.ApplyScriptContextChanges)
            .TapAsync(_ => instanceRepository.UpdateAsync(context.Instance, true, cancellationToken))
            .Map(_ => StepOutcome.Continue());
    }

    /// <summary>
    /// Checks if context has OnExecute tasks.
    /// </summary>
    private static bool HasOnExecuteTasks(TransitionExecutionContext context)
        => context.Transition != null && context.Transition.OnExecutionTasks.Any();

    /// <summary>
    /// Builds or retrieves script context.
    /// </summary>
    private async Task<ScriptContext> BuildScriptContextAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await context.GetOrBuildScriptContextAsync(
            ct => CreateScriptContextAsync(context, ct),
            cancellationToken);
    }

    /// <summary>
    /// Executes the OnExecute tasks.
    /// </summary>
    private async Task ExecuteTasksAsync(
        TransitionExecutionContext context,
        ScriptContext scriptContext,
        CancellationToken cancellationToken)
    {
        var instanceTransitionId = GetTransitionRecordId(context);

        await taskOrchestrationService.ExecuteAsync(
            context.Transition!.OnExecutionTasks,
            instanceTransitionId,
            TaskTrigger.OnExecute,
            scriptContext,
            cancellationToken);
    }

    /// <summary>
    /// Gets transition record ID from context items.
    /// </summary>
    private static Guid? GetTransitionRecordId(TransitionExecutionContext context)
        => context.Items.TryGetValue("TransitionRecordId", out var record) ? record as Guid? : null;

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
