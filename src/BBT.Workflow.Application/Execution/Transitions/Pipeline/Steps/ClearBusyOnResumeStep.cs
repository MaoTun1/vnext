using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

public sealed class ClearBusyOnResumeStep(
    IInstanceRepository instanceRepository) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.ClearBusyOnResumeStep;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ClearBusyOnResumeStep)}");

        // Only process this step if resuming from SubFlow completion
        // Note: ResumeFromOrder is consumed by the planner before steps execute,
        // so we only check IsSubFlowResume flag here
        if (!context.Directives.IsSubFlowResume)
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway Oriented Programming: Chain operations, each wrapped in Try
        return await SetInstanceActive(context, cancellationToken)
            .ThenAsync(() => UpdateTargetStateInContext(context));
    }

    /// <summary>
    /// Sets instance to active state and updates in repository.
    /// </summary>
    private async Task<Result> SetInstanceActive(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Set instance active
        context.Instance.Active();
        //TODO: UpdateStatus'den Update'e dönüş yaptık.
        // Update repository - wrapped in Try
        var updateResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.UpdateAsync(context.Instance, true, ct),
            cancellationToken);
        
        return updateResult.IsSuccess ? Result.Ok() : Result.Fail(updateResult.Error);
    }

    /// <summary>
    /// Updates target state in context using Result Pattern and returns the final step outcome.
    /// </summary>
    private Task<Result<StepOutcome>> UpdateTargetStateInContext(TransitionExecutionContext context)
    {
        var targetStateResult = context.Workflow.GetState(context.Instance.GetCurrentState);

        if (!targetStateResult.IsSuccess)
        {
            return Task.FromResult(Result<StepOutcome>.Fail(targetStateResult.Error));
        }

        context.Target = targetStateResult.Value!;

        return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue()));
    }
}