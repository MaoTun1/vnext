using BBT.Aether.BackgroundJob;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Payloads;
using Dapr.Jobs.Models;
using Microsoft.Extensions.Logging;
using BBT.Aether.Results;
using BBT.Workflow.BackgroundJobs.Handlers;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Asynchronous transition execution strategy.
/// Executes transitions as background jobs for better scalability and fault tolerance.
/// </summary>
public sealed class AsyncTransitionStrategy(
    IBackgroundJobService backgroundJobService,
    ITransitionContextFactory ctxFactory,
    IInstanceJobRepository jobRepository,
    ILogger<AsyncTransitionStrategy> logger) : ITransitionStrategy
{
    /// <inheritdoc />
    public async Task<Result<TransitionExecutionContext>> ExecuteAsync(WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Railway: Create context -> Enqueue job
        var ctxResult = await ctxFactory.CreateAsync(context, cancellationToken);
        if (!ctxResult.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(ctxResult.Error);

        var ctx = ctxResult.Value!;

        var enqueueResult = await EnqueueTransitionJobAsync(context, cancellationToken);
        if (!enqueueResult.IsSuccess)
        {
            logger.LogError("Asynchronous transition execution failed for {TransitionKey} on instance {InstanceId}",
                context.TransitionKey, context.InstanceId);
            return Result<TransitionExecutionContext>.Fail(enqueueResult.Error);
        }

        logger.LogInformation(
            "Successfully enqueued transition {TransitionKey} for instance {InstanceId} with job ID {JobId}",
            context.TransitionKey, context.InstanceId, enqueueResult.Value);

        return Result<TransitionExecutionContext>.Ok(ctx);
    }

    private async Task<Result<string>> EnqueueTransitionJobAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var jobName = $"trans-{context.InstanceId}-{context.TransitionKey}";
        var jobPayload = new TransitionJobPayload
        {
            JobName = jobName,
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
        
        var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow).ExpressionValue;

        var metadata = new Dictionary<string, object>
        {
            ["domain"] = context.Domain,
            ["flowName"] = context.WorkflowKey,
            ["instanceId"] = context.InstanceId.ToString()
        };

        var enqueueResult = await ResultExtensions.TryAsync(
            async ct => await backgroundJobService.EnqueueAsync(
                TransitionJobHandler.HandlerName,
                jobName,
                jobPayload,
                schedule,
                metadata,
                ct),
            cancellationToken);

        if (!enqueueResult.IsSuccess)
            return Result<string>.Fail(enqueueResult.Error);

        await jobRepository.InsertAsync(
            InstanceJob.Create(
                enqueueResult.Value,
                jobName,
                enqueueResult.Value,
                context.Domain,
                context.WorkflowKey,
                context.InstanceId
            ),
            true,
            cancellationToken
        );

        return Result<string>.Ok(jobName);
    }
}