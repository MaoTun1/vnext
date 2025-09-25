using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Dapr.Jobs.Models;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Implements asynchronous transition execution strategy.
/// Validates and enqueues the workflow transition operation as a background job.
/// </summary>
public sealed class AsyncTransitionStrategy(
    IBackgroundJobService backgroundJobService,
    ICurrentSchema currentSchema,
    IInstanceRepository instanceRepository,
    ILogger<AsyncTransitionStrategy> logger) : ITransitionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(bool isSync) => !isSync;

    /// <inheritdoc />
    public async Task<InstanceServiceResponse<TransitionOutput>> ExecuteAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Executing asynchronous transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        using (currentSchema.Change(context.Input.Workflow))
        {
            // Set instance to busy to reserve it for background processing
            context.Instance.Busy();
            await instanceRepository.UpdateAsync(context.Instance, true, cancellationToken);

            logger.LogInformation(
                "Reserved instance {InstanceId} for async transition {TransitionKey}",
                context.InstanceId, context.TransitionKey);

            // Now enqueue the background job
            return await EnqueueTransitionJobAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Enqueues a transition operation as a background job.
    /// </summary>
    private async Task<InstanceServiceResponse<TransitionOutput>> EnqueueTransitionJobAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var jobId = $"transition-{context.InstanceId}-{context.TransitionKey}-{DateTimeOffset.UtcNow.Ticks}";

        // Create job payload
        var payload = new TransitionJobPayload
        {
            InstanceId = context.InstanceId,
            TransitionKey = context.TransitionKey,
            Domain = context.Input.Domain,
            Workflow = context.Input.Workflow,
            Version = context.Input.Version,
            Data = context.Input.Data,
            Headers = context.Input.Headers,
            RouteValues = context.Input.RouteValues,
            ExecutionContext = context.Input.ExecutionContext
        };

        // Create job metadata
        var jobMetadata = new Dictionary<string, string>
        {
            ["domain"] = context.Input.Domain,
            ["flowName"] = context.Input.Workflow,
            ["instanceId"] = context.InstanceId.ToString()
        };

        // Schedule job to run immediately
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(1));

        await backgroundJobService.EnqueueAsync(
            BackgroundJobConsts.TransitionJobName,
            jobId,
            schedule,
            payload,
            jobMetadata,
            cancellationToken);

        logger.LogInformation(
            "Enqueued transition job {JobId} for instance {InstanceId} with transition {TransitionKey}",
            jobId, context.InstanceId, context.TransitionKey);

        // Return response with Busy status to indicate background processing
        return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
        {
            Id = context.InstanceId,
            Status = InstanceStatus.Busy // Indicate that the transition is being processed in background
        });
    }
}