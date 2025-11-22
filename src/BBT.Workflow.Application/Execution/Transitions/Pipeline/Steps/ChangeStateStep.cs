using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;

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
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ChangeStateStep)}");

        // Skip for SubFlow resume - state already changed
        if (context.Directives.IsSubFlowResume || context.Transition == null)
        {
            logger.LogDebug("Skipping state change for SubFlow resume on instance {InstanceId}",
                context.InstanceId);
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway Oriented Programming: Chain operations, each wrapped in Try
        return await GetStateTransitionInfo(context)
            .ThenAsync(info => RecordTransitionMetric(context, info))
            .ThenAsync(info => PerformStateChange(context, info, cancellationToken))
            .ThenAsync(_ => UpdateTargetStateInContext(context))
            .OnSuccess(_ => RecordStateEntryMetric(context))
            .OnSuccess(info => LogStateChange(context, info))
            .OnSuccess(AddTelemetryEvent)
            .ThenAsync(_ => Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue())));
    }

    /// <summary>
    /// Gets state transition information from context.
    /// </summary>
    private Task<Result<StateTransitionInfo>> GetStateTransitionInfo(TransitionExecutionContext context)
    {
        return Task.FromResult(
            ResultExtensions.Try(() =>
            {
                var fromState = context.Instance.GetCurrentState;
                var toState = context.Transition!.Target;

                return new StateTransitionInfo(fromState, toState, context.Transition);
            })
        );
    }

    /// <summary>
    /// Records state transition metric.
    /// </summary>
    private Task<Result<StateTransitionInfo>> RecordTransitionMetric(
        TransitionExecutionContext context,
        StateTransitionInfo info)
    {
        return Task.FromResult(
            ResultExtensions.Try(() =>
            {
                workflowMetrics.RecordStateTransition(
                    context.Workflow.Key,
                    info.FromState,
                    info.ToState);

                return info;
            })
        );
    }

    /// <summary>
    /// Performs the actual state change and updates the instance in repository.
    /// </summary>
    private async Task<Result<StateTransitionInfo>> PerformStateChange(
        TransitionExecutionContext context,
        StateTransitionInfo info,
        CancellationToken cancellationToken)
    {
        // Change state
        context.Instance.ChangeState(info.Transition);

        // Update repository - wrapped in Try
        var updateResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.UpdateAsync(context.Instance, true, ct),
            cancellationToken);

        return updateResult.IsSuccess
            ? Result<StateTransitionInfo>.Ok(info)
            : Result<StateTransitionInfo>.Fail(updateResult.Error);
    }

    /// <summary>
    /// Updates target state in context using Result Pattern.
    /// </summary>
    private Task<Result<StateTransitionInfo>> UpdateTargetStateInContext(TransitionExecutionContext context)
    {
        var targetStateResult = context.Workflow.GetState(context.Instance.GetCurrentState);

        if (!targetStateResult.IsSuccess)
        {
            return Task.FromResult(
                Result<StateTransitionInfo>.Fail(targetStateResult.Error)
            );
        }

        context.Target = targetStateResult.Value!;

        var info = new StateTransitionInfo(
            context.Instance.GetCurrentState,
            context.Instance.GetCurrentState,
            context.Transition!);

        return Task.FromResult(Result<StateTransitionInfo>.Ok(info));
    }

    /// <summary>
    /// Records state entry metric.
    /// </summary>
    private void RecordStateEntryMetric(TransitionExecutionContext context)
    {
        workflowMetrics.RecordStateEntry(
            context.Workflow.Key,
            context.Instance.GetCurrentState);
    }

    /// <summary>
    /// Logs state change with structured logging.
    /// </summary>
    private void LogStateChange(TransitionExecutionContext context, StateTransitionInfo info)
    {
        logger.StateChanged(
            TelemetryConstants.Prefixes.Execution,
            info.FromState,
            info.ToState,
            context.InstanceId);
    }

    /// <summary>
    /// Adds state changed event to telemetry span.
    /// </summary>
    private void AddTelemetryEvent(StateTransitionInfo info)
    {
        Activity.Current?.AddEvent(new ActivityEvent("state.changed",
            tags: new ActivityTagsCollection
            {
                { TelemetryConstants.TagNames.StateFrom, info.FromState },
                { TelemetryConstants.TagNames.StateTo, info.ToState }
            }));
    }

    /// <summary>
    /// Encapsulates state transition information.
    /// </summary>
    private sealed record StateTransitionInfo(
        string FromState,
        string ToState,
        Definitions.Transition Transition);
}