using BBT.Workflow.Domain;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Pipeline.Steps;

public sealed class ClearBusyOnResumeStep(
    IInstanceRepository instanceRepository) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.ClearBusyOnResumeStep;

    /// <inheritdoc />
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Only process this step if resuming from SubFlow completion
        // Note: ResumeFromOrder is consumed by the planner before steps execute,
        // so we only check IsSubFlowResume flag here
        if (context.Directives.IsSubFlowResume)
        {
            context.Instance.Active();

            await instanceRepository.UpdateStatusAsync(context.Instance, cancellationToken);
            
            // Get target state using Result Pattern
            var targetStateResult = context.Workflow.GetState(context.Instance.GetCurrentState);
            if (!targetStateResult.IsSuccess)
                return Result<StepOutcome>.Fail(targetStateResult.Error);
            
            context.Target = targetStateResult.Value!;
        }
        return Result<StepOutcome>.Ok(StepOutcome.Continue());
    }
}