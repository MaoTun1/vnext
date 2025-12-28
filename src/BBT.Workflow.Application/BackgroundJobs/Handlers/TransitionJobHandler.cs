using System.Diagnostics;
using BBT.Aether.BackgroundJob;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles asynchronous transition background jobs.
/// This handler processes workflow transition requests that were submitted with Sync=true.
/// </summary>
public sealed class TransitionJobHandler(
    IInstanceJobRepository jobRepository,
    IWorkflowExecutionService workflowExecutionService,
    ILogger<TransitionJobHandler> logger) : IBackgroundJobHandler<TransitionJobPayload>
{
    public const string HandlerName = "flow.transition";

    /// <summary>
    /// ActivitySource for creating activities linked to the original trace context.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new("BBT.Workflow.BackgroundJobs");

    public async Task HandleAsync(TransitionJobPayload args, CancellationToken cancellationToken)
    {
        // Restore trace context from the original request for distributed tracing correlation
        using var activity = StartActivityWithTraceContext(args);

        try
        {
            EnrichActivity(activity, args);

            // For async processing, instance should already be pre-reserved and in Busy status
            // Reconstruct the original TransitionInput with Sync=true
            var transitionInput = new TransitionInput(
                    args.Domain,
                    args.Workflow,
                    args.Version,
                    new TransitionDataInput(args.Data)
                    {
                        Key = args.InstanceKey,
                        Tags = args.Tags
                    },
                    sync: true) // Force sync=true to avoid infinite loop
                {
                    Headers = args.Headers,
                    RouteValues = args.RouteValues
                };

            var context =
                transitionInput.ToExecutionContext(args.InstanceId.ToString(), args.TransitionKey);
            context.Actor = args.ExecutionActor;

            // Use the background-specific method that handles pre-reserved instances
            var result = await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);
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
    private static Activity? StartActivityWithTraceContext(TransitionJobPayload args)
    {
        ActivityContext parentContext = default;

        // Try to restore the parent trace context from the payload
        if (!string.IsNullOrEmpty(args.TraceParent) &&
            ActivityContext.TryParse(args.TraceParent, args.TraceState, out var parsedContext))
        {
            parentContext = parsedContext;
        }

        return ActivitySource.StartActivity(
            "TransitionJob.Execute",
            ActivityKind.Consumer,
            parentContext);
    }

    /// <summary>
    /// Enriches the activity with job-specific tags for observability.
    /// </summary>
    private static void EnrichActivity(Activity? activity, TransitionJobPayload args)
    {
        if (activity is null) return;

        activity.SetTag(TelemetryConstants.TagNames.Domain, args.Domain);
        activity.SetTag(TelemetryConstants.TagNames.Flow, args.Workflow);
        activity.SetTag(TelemetryConstants.TagNames.FlowVersion, args.Version);
        activity.SetTag(TelemetryConstants.TagNames.InstanceId, args.InstanceId);
        activity.SetTag(TelemetryConstants.TagNames.TransitionKey, args.TransitionKey);
        activity.SetTag(TelemetryConstants.TagNames.JobName, args.JobName);
        activity.SetTag("messaging.system", "dapr");
        activity.SetTag("messaging.operation", "process");
    }
}