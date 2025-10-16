using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        var fromState = context.Instance.GetCurrentState;
        var toState = context.Transition.Target;
        
        logger.LogDebug("Changing state from {FromState} to {ToState} for instance {InstanceId}",
            fromState, toState, context.InstanceId);

        // Record state transition metric
        workflowMetrics.RecordStateTransition(
            context.Workflow.Key,
            fromState,
            toState);

        // Perform the state change
        context.Instance.ChangeState(context.Transition);
        
        // Update instance with Result pattern (no exceptions)
        var updateResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.UpdateAsync(context.Instance, true, ct),
            cancellationToken,
            ex => Error.Dependency("db.update", $"Failed to update instance state: {ex.Message}"));
        
        if (!updateResult.IsSuccess)
            return Result<StepOutcome>.Fail(updateResult.Error);
        
        // Update target state in context using Result Pattern
        var targetStateResult = context.Workflow.GetState(context.Instance.GetCurrentState);
        if (!targetStateResult.IsSuccess)
            return Result<StepOutcome>.Fail(targetStateResult.Error);
        
        context.Target = targetStateResult.Value!;
        
        // Record state entry metric
        workflowMetrics.RecordStateEntry(
            context.Workflow.Key,
            context.Instance.GetCurrentState);

        // Log state change with structured logging
        logger.StateChanged(
            TelemetryConstants.Prefixes.Execution,
            fromState,
            toState,
            context.InstanceId);
        
        // Add state changed event to current span
        Activity.Current?.AddEvent(new ActivityEvent("state.changed", 
            tags: new ActivityTagsCollection
            {
                { TelemetryConstants.TagNames.StateFrom, fromState },
                { TelemetryConstants.TagNames.StateTo, toState }
            }));
        
        return Result<StepOutcome>.Ok(StepOutcome.Continue());
    }
}
