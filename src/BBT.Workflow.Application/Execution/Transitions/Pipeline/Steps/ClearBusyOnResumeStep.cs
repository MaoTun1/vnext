using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that clears the busy state when resuming from SubFlow completion.
/// </summary>
public sealed class ClearBusyOnResumeStep(
    IInstanceRepository instanceRepository) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.ClearBusyOnResumeStep;

    /// <inheritdoc />
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ClearBusyOnResumeStep)}");

        // Only process this step if resuming from SubFlow completion
        if (!context.Directives.IsSubFlowResume)
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway chain: Set active -> Update repo -> Update target state
        return await Result.Ok(context)
            .Tap(ctx => ctx.Instance.Active())
            .TapAsync(ctx => instanceRepository.UpdateAsync(ctx.Instance, true, cancellationToken))
            .Bind(UpdateTargetStateInContext)
            .Map(_ => StepOutcome.Continue());
    }

    /// <summary>
    /// Updates target state in context.
    /// </summary>
    private static Result<TransitionExecutionContext> UpdateTargetStateInContext(TransitionExecutionContext context)
    {
        return context.Workflow.GetState(context.Instance.GetCurrentState)
            .Map(state =>
            {
                context.Target = state;
                return context;
            });
    }
}
