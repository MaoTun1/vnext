using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that schedules future transitions based on timers.
/// Enqueues scheduled transitions for later execution.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class ScheduleTransitionsStep(
    IBackgroundJobService backgroundJobService,
    ITaskTimerService taskTimerService,
    IScriptContextFactory scriptContextFactory,
    ILogger<ScheduleTransitionsStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Schedule;

    /// <inheritdoc />
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target?.ScheduledTransitions == null || !context.Target.ScheduledTransitions.Any())
        {
            logger.LogTrace("No scheduled transitions for state {StateName}", context.Target?.Key ?? "Unknown");
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        logger.LogDebug("Scheduling {Count} transitions for state {StateName} on instance {InstanceId}",
            context.Target.ScheduledTransitions.Count(), context.Target.Key, context.InstanceId);
        
        return await ResultExtensions.TryAsync<StepOutcome>(async ct =>
        {
            foreach (var scheduledTransition in context.Target.ScheduledTransitions)
            {
                try
                {
                    await ScheduleTransitionAsync(context, scheduledTransition, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to schedule transition {TransitionKey} for instance {InstanceId}",
                        scheduledTransition.Key, context.InstanceId);
                    // Continue with other scheduled transitions
                }
            }
            
            // context.SkipImmediateExecution = true;

            logger.LogDebug("Completed scheduling transitions for state {StateName}", context.Target.Key);
            return StepOutcome.Continue();
        },
        cancellationToken,
        ex => Error.Failure(
            WorkflowErrorCodes.ExecutionStepFailed,
            $"Failed to schedule transitions: {ex.Message}",
            ex.GetType().Name));
    }

    /// <summary>
    /// Schedules a single transition for future execution.
    /// </summary>
    private async Task ScheduleTransitionAsync(
        TransitionExecutionContext context,
        Transition scheduledTransition,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Scheduling transition {TransitionKey} for instance {InstanceId}",
            scheduledTransition.Key, context.InstanceId);

        if (scheduledTransition.Timer == null)
        {
            logger.LogWarning("Transition {TransitionKey} has no timer defined, skipping scheduling",
                scheduledTransition.Key);
            return;
        }

        try
        {
            // Build script context for timer evaluation
            var scriptContext = await scriptContextFactory
                .NewBuilder()
                .WithWorkflow(context.Workflow)
                .WithInstance(context.Instance)
                .WithTransition(scheduledTransition)
                .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                .WithRouteValues(context.RouteValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                .BuildAsync(cancellationToken);

            // Evaluate timer to get the schedule
            var timerSchedule = await taskTimerService.ExecuteTimerAsync(
                scheduledTransition.Timer,
                scriptContext,
                cancellationToken);

            logger.LogDebug("Timer evaluation for transition {TransitionKey} resulted in schedule: {ScheduleType}",
                scheduledTransition.Key, timerSchedule.ScheduleType);

            // Enqueue the transition timer job
            await backgroundJobService.EnqueueTransitionTimerAsync(
                context.InstanceId,
                context.WorkflowKey,
                context.Domain,
                context.Workflow.Version,
                scheduledTransition.Key,
                timerSchedule,
                cancellationToken);

            logger.LogTrace("Scheduled transition {TransitionKey} for instance {InstanceId} with timer schedule",
                scheduledTransition.Key, context.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to evaluate timer for transition {TransitionKey} on instance {InstanceId}",
                scheduledTransition.Key, context.InstanceId);
            throw;
        }
    }
}
