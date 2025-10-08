using BBT.Workflow.Execution;
using BBT.Workflow.Execution.Pipeline;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that performs the core state transition.
/// This step changes the instance state and updates the target state in context.
/// </summary>
public sealed class ChangeStateStep(
    IInstanceRepository instanceRepository,
    IWorkflowMetrics workflowMetrics,
    ILogger<ChangeStateStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.ChangeState;

    /// <inheritdoc />
    public async Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogDebug("Changing state from {FromState} to {ToState} for instance {InstanceId}",
            context.Instance.GetCurrentState, context.Transition.Target, context.InstanceId);

        // Record state transition metric
        workflowMetrics.RecordStateTransition(
            context.Workflow.Key,
            context.Instance.GetCurrentState,
            context.Transition.Target);

        // Perform the state change
        context.Instance.ChangeState(context.Transition);
        await instanceRepository.UpdateAsync(context.Instance, true, cancellationToken);
        
        // Update target state in context
        context.Target = context.Workflow.GetState(context.Instance.GetCurrentState);
        
        // Record state entry metric
        workflowMetrics.RecordStateEntry(
            context.Workflow.Key,
            context.Instance.GetCurrentState);

        logger.LogDebug("State changed to {NewState} for instance {InstanceId}",
            context.Instance.GetCurrentState, context.InstanceId);
    }
}
