using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Telemetry;
using Dapr.Jobs.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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
        var sw = Stopwatch.StartNew();
        
        logger.StrategyExecutionStarted(
            TelemetryConstants.Prefixes.Execution,
            nameof(AsyncTransitionStrategy),
            context.TransitionKey,
            context.InstanceId);

        try
        {
            // 1. Create execution context (for state reservation)
            var ctx = await ctxFactory.CreateAsync(context, cancellationToken); // rehydrate
            
            logger.ContextCreated(
                TelemetryConstants.Prefixes.Execution,
                context.TransitionKey,
                "BackgroundJob");

            // 2. Prepare job payload
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
                ["flowName"] = context.WorkflowKey,
                ["instanceId"] = context.InstanceId.ToString()
            };

            // 3. Enqueue background job for actual execution
            await backgroundJobService.EnqueueAsync(
                BackgroundJobConsts.TransitionJobName,
                jobId,
                schedule,
                jobPayload,
                metadata,
                cancellationToken);

            sw.Stop();
            logger.StrategyExecutionCompleted(
                TelemetryConstants.Prefixes.Execution,
                nameof(AsyncTransitionStrategy),
                context.TransitionKey,
                sw.ElapsedMilliseconds);
            
            logger.LogInformation("Successfully enqueued transition {TransitionKey} for instance {InstanceId} with job ID {JobId}",
                context.TransitionKey, context.InstanceId, jobId);

            return ctx;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Asynchronous transition execution failed for {TransitionKey} on instance {InstanceId}",
                context.TransitionKey, context.InstanceId);
            throw;
        }
    }
}
