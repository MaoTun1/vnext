using System.Diagnostics;
using BBT.Aether.BackgroundJob;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Caching;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles workflow timeout jobs that are triggered when workflow instances exceed their configured timeout duration.
/// This handler is responsible for processing timeout events and transitioning workflow instances to their timeout state.
/// </summary>
public sealed class FlowTimeoutJobHandler(
    IInstanceRepository instanceRepository,
    IInstanceJobRepository jobRepository,
    IComponentCacheStore componentCacheStore,
    IWorkflowMetrics workflowMetrics,
    IRuntimeInfoProvider runtimeInfoProvider,
    ILogger<FlowTimeoutJobHandler> logger
) : IBackgroundJobHandler<WorkflowTimeoutPayload>
{
    public const string HandlerName = "flow.timeout";

    /// <summary>
    /// ActivitySource for creating activities linked to the original trace context.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new("BBT.Workflow.BackgroundJobs");

    public async Task HandleAsync(WorkflowTimeoutPayload args, CancellationToken cancellationToken)
    {
        // Restore trace context from the original request for distributed tracing correlation
        using var activity = StartActivityWithTraceContext(args);

        try
        {
            EnrichActivity(activity, args);

            var workflowResult = await componentCacheStore.GetFlowAsync(args.Domain, args.FlowName,
                args.Version, cancellationToken);

            if (!workflowResult.IsSuccess)
            {
                logger.WorkflowNotFoundWarning(args.FlowName, workflowResult.Error.Code);
                activity?.SetStatus(ActivityStatusCode.Error, "Workflow not found");
                return;
            }

            var workflow = workflowResult.Value!;

            var instance =
                await instanceRepository.FindAsync(p => p.Id == args.InstanceId, true,
                    cancellationToken);
            if (instance == null)
            {
                logger.InstanceNotFound(args.InstanceId, args.FlowName);
                activity?.SetStatus(ActivityStatusCode.Error, "Instance not found");
                return;
            }

            if (instance.IsActive || instance.IsBusy)
            {
                // Record current status before timeout
                var currentStatus = instance.Status.Code;

                if (workflow.Timeout is null)
                {
                    logger.TimeoutConfigMissing(instance.Flow);
                    activity?.SetStatus(ActivityStatusCode.Error, "Timeout config missing");
                    return;
                }

                instance.ChangeState(workflow.Timeout!);
                instance.Complete(); // This calculates the Duration

                // Record timeout metrics with duration - this will also decrement the current status gauge
                var durationSeconds = instance.Duration?.TotalSeconds;
                workflowMetrics.RecordInstanceTimedOut(instance.Flow, runtimeInfoProvider.Domain, currentStatus,
                    durationSeconds);
                await instanceRepository.UpdateAsync(instance, true, cancellationToken);
                await jobRepository.MarkAsProcessedAsync(args.JobName, cancellationToken);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Job cancelled");
            logger.JobCancelled(args.JobName, "timeout", args.InstanceId);
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
    private static Activity? StartActivityWithTraceContext(WorkflowTimeoutPayload args)
    {
        ActivityContext parentContext = default;

        // Try to restore the parent trace context from the payload
        if (!string.IsNullOrEmpty(args.TraceParent) &&
            ActivityContext.TryParse(args.TraceParent, args.TraceState, out var parsedContext))
        {
            parentContext = parsedContext;
        }

        return ActivitySource.StartActivity(
            "TimeoutJob.Execute",
            ActivityKind.Consumer,
            parentContext);
    }

    /// <summary>
    /// Enriches the activity with job-specific tags for observability.
    /// </summary>
    private static void EnrichActivity(Activity? activity, WorkflowTimeoutPayload args)
    {
        if (activity is null) return;

        activity.SetTag(TelemetryConstants.TagNames.Domain, args.Domain);
        activity.SetTag(TelemetryConstants.TagNames.Flow, args.FlowName);
        activity.SetTag(TelemetryConstants.TagNames.FlowVersion, args.Version);
        activity.SetTag(TelemetryConstants.TagNames.InstanceId, args.InstanceId);
        activity.SetTag(TelemetryConstants.TagNames.JobName, args.JobName);
        activity.SetTag("messaging.system", "dapr");
        activity.SetTag("messaging.operation", "process");
    }
}