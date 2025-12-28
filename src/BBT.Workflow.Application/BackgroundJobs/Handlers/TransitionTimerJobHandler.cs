using System.Diagnostics;
using BBT.Aether.BackgroundJob;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using BBT.Workflow.Shared;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

public sealed class TransitionTimerJobHandler(
    IWorkflowExecutionService workflowExecutionService,
    IInstanceJobRepository jobRepository,
    ILogger<TransitionTimerJobHandler> logger) : IBackgroundJobHandler<TransitionTimerPayload>
{
    public const string HandlerName = "flow.transition.schedule";

    /// <summary>
    /// ActivitySource for creating activities linked to the original trace context.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new("BBT.Workflow.BackgroundJobs");

    public async Task HandleAsync(TransitionTimerPayload args, CancellationToken cancellationToken)
    {
        // Restore trace context from the original request for distributed tracing correlation
        using var activity = StartActivityWithTraceContext(args);

        try
        {
            EnrichActivity(activity, args);

            var input = new TransitionInput(
                args.Domain,
                args.FlowName,
                args.Version
            );

            // Convert TransitionInput to WorkflowExecutionContext
            var executionContext = input.ToExecutionContext(
                args.InstanceId.ToString(),
                args.TransitionKey);

            // Override trigger type to Scheduled for timer-based transitions
            executionContext.TriggerType = TriggerType.Scheduled;
            executionContext.Actor = ExecutionActor.System;
            executionContext.IsReentry = true; // Timer transitions are re-entry executions

            await workflowExecutionService.ExecuteTransitionAsync(
                executionContext,
                cancellationToken
            );

            await jobRepository.MarkAsProcessedAsync(args.JobName, cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            logger.JobCompleted(args.JobName, args.TransitionKey, args.InstanceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Job cancelled");
            logger.JobCancelled(args.JobName, args.TransitionKey, args.InstanceId);
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddTag("error.type", e.GetType().Name);
            logger.JobFailed(e, args.JobName, args.InstanceId);
            throw;
        }
    }

    /// <summary>
    /// Starts a new activity linked to the original trace context from the payload.
    /// This ensures the background job execution is correlated with the original request trace.
    /// </summary>
    private static Activity? StartActivityWithTraceContext(TransitionTimerPayload args)
    {
        ActivityContext parentContext = default;

        // Try to restore the parent trace context from the payload
        if (!string.IsNullOrEmpty(args.TraceParent) &&
            ActivityContext.TryParse(args.TraceParent, args.TraceState, out var parsedContext))
        {
            parentContext = parsedContext;
        }

        return ActivitySource.StartActivity(
            "TransitionTimerJob.Execute",
            ActivityKind.Consumer,
            parentContext);
    }

    /// <summary>
    /// Enriches the activity with job-specific tags for observability.
    /// </summary>
    private static void EnrichActivity(Activity? activity, TransitionTimerPayload args)
    {
        if (activity is null) return;

        activity.SetTag(TelemetryConstants.TagNames.Domain, args.Domain);
        activity.SetTag(TelemetryConstants.TagNames.Flow, args.FlowName);
        activity.SetTag(TelemetryConstants.TagNames.FlowVersion, args.Version);
        activity.SetTag(TelemetryConstants.TagNames.InstanceId, args.InstanceId);
        activity.SetTag(TelemetryConstants.TagNames.TransitionKey, args.TransitionKey);
        activity.SetTag(TelemetryConstants.TagNames.JobName, args.JobName);
        activity.SetTag("messaging.system", "dapr");
        activity.SetTag("messaging.operation", "process");
    }
}