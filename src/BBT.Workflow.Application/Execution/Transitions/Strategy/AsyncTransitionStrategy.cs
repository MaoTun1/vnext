using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Payloads;
using Dapr.Jobs.Models;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Asynchronous transition execution strategy.
/// Executes transitions as background jobs for better scalability and fault tolerance.
/// </summary>
public sealed class AsyncTransitionStrategy(
    IBackgroundJobService backgroundJobService,
    ITransitionContextFactory ctxFactory,
    ILogger<AsyncTransitionStrategy> logger) : ITransitionStrategy
{
    /// <inheritdoc />
    public async Task<TransitionExecutionContext> ExecuteAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogDebug("Executing transition asynchronously");

        try
        {
            var ctx = await ctxFactory.CreateAsync(context, cancellationToken); // rehydrate

            var jobPayload = new TransitionJobPayload
            {
                InstanceId = context.InstanceId,
                TransitionKey = context.TransitionKey,
                Domain = context.Domain,
                Workflow = context.WorkflowKey,
                Version = context.WorkflowVersion,
                Data = context.Data,
                Headers = context.Headers,
                RouteValues = context.RouteValues,
                ExecutionActor = context.Actor
            };

            var jobId = $"transition-{context.InstanceId}-{context.TransitionKey}-{Guid.NewGuid():N}";
            var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow); // Execute immediately

            var metadata = new Dictionary<string, string>
            {
                ["domain"] = context.Domain,
                ["workflow"] = context.WorkflowKey,
                ["instanceId"] = context.InstanceId.ToString()
            };

            await backgroundJobService.EnqueueAsync(
                BackgroundJobConsts.TransitionJobName,
                jobId,
                schedule,
                jobPayload,
                metadata,
                cancellationToken);

            logger.LogInformation("Successfully enqueued transition {TransitionKey} for instance {InstanceId} with job ID {JobId}",
                context.TransitionKey, context.InstanceId, jobId);
            
            logger.LogDebug("Asynchronous transition execution completed successfully");

            return ctx;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Asynchronous transition execution failed");
            throw;
        }
    }
}
