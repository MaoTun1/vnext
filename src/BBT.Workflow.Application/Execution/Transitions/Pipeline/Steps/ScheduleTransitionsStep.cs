using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.BackgroundJob;
using BBT.Aether.Results;
using BBT.Workflow.BackgroundJobs.Handlers;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using BBT.Workflow.Logging;
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
    IInstanceJobRepository jobRepository,
    ILogger<ScheduleTransitionsStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Schedule;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ScheduleTransitionsStep)}");

        if (context.Target?.ScheduledTransitions == null || !context.Target.ScheduledTransitions.Any())
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        return await ResultExtensions.TryAsync<StepOutcome>(async ct =>
            {
                foreach (var scheduledTransition in context.Target.ScheduledTransitions)
                {
                    // Let outer Try wrapper handle all exceptions
                    await ScheduleTransitionAsync(context, scheduledTransition, ct);
                }

                return StepOutcome.Continue();
            },
            cancellationToken);
    }

    /// <summary>
    /// Schedules a single transition for future execution.
    /// </summary>
    private async Task ScheduleTransitionAsync(
        TransitionExecutionContext context,
        Transition scheduledTransition,
        CancellationToken cancellationToken)
    {
        if (scheduledTransition.Timer == null)
        {
            logger.LogWarning("Transition {TransitionKey} has no timer defined, skipping scheduling",
                scheduledTransition.Key);
            return;
        }

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

        var jobName = $"trans-{context.InstanceId}-{context.TransitionKey}";
        var payload = new TransitionTimerPayload
        {
            JobName = jobName,
            Domain = context.Domain,
            FlowName = context.WorkflowKey,
            Version = context.Workflow.Version,
            TransitionKey = scheduledTransition.Key,
            InstanceId = context.InstanceId
        };

        var metadata = new Dictionary<string, object>
        {
            ["domain"] = context.Domain,
            ["flowName"] = context.WorkflowKey,
            ["instanceId"] = context.InstanceId.ToString()
        };

        // Enqueue the transition timer job
        var jobId = await backgroundJobService.EnqueueAsync(
            TransitionTimerJobHandler.HandlerName,
            jobName,
            payload,
            timerSchedule.ToDaprJobSchedule().ExpressionValue,
            metadata: metadata,
            cancellationToken);

        await jobRepository.InsertAsync(
            InstanceJob.Create(
                jobId,
                jobName,
                jobId,
                context.Domain,
                context.WorkflowKey,
                context.InstanceId),
            true,
            cancellationToken);
    }
}