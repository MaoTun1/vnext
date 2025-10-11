using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Pipeline.Steps;

public sealed class ClearBusyOnResumeStep(
    IInstanceRepository instanceRepository) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.ClearBusyOnResumeStep;

    /// <inheritdoc />
    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Only process this step if resuming from SubFlow completion
        // Note: ResumeFromOrder is consumed by the planner before steps execute,
        // so we only check IsSubFlowResume flag here
        if (context.Directives.IsSubFlowResume)
        {
            context.Instance.Active();
            await instanceRepository.UpdateStatusAsync(context.Instance, cancellationToken);
            context.Target = context.Workflow.GetState(context.Instance.GetCurrentState);
        }
        return StepOutcome.Continue();
    }
}