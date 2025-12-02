using BBT.Aether.BackgroundJob;
using BBT.Aether.Results;
using BBT.Workflow.BackgroundJobs.Handlers;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
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
    IInstanceJobRepository jobRepository,
    ILogger<AsyncTransitionStrategy> logger) : ITransitionStrategy
{
    /// <inheritdoc />
    /// <summary>
    /// Executes transition asynchronously by enqueuing a background job.
    /// Railway chain: Create Context → Enqueue Job → Return Context
    /// </summary>
    public Task<Result<TransitionExecutionContext>> ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        return ctxFactory.CreateAsync(context, cancellationToken)
            .BindAsync(ctx => EnqueueJobAndReturnContextAsync(ctx, context, cancellationToken));
    }

    /// <summary>
    /// Enqueues the job and returns the original context on success.
    /// Uses Match for idiomatic Result handling with logging side effects.
    /// </summary>
    private async Task<Result<TransitionExecutionContext>> EnqueueJobAndReturnContextAsync(
        TransitionExecutionContext ctx,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var enqueueResult = await EnqueueAndSaveJobAsync(context, ctx, cancellationToken);

        return enqueueResult.Match(
            onSuccess: jobName =>
            {
                LogEnqueueSuccess(context, jobName);
                return Result<TransitionExecutionContext>.Ok(ctx);
            },
            onFailure: error =>
            {
                LogEnqueueFailure(context);
                return Result<TransitionExecutionContext>.Fail(error);
            });
    }

    /// <summary>
    /// Enqueues the transition job to Dapr and saves the job record.
    /// Railway chain: Build Payload → Enqueue to Dapr → Save Record
    /// </summary>
    private async Task<Result<string>> EnqueueAndSaveJobAsync(
        WorkflowExecutionContext context,
        TransitionExecutionContext transContext,
        CancellationToken cancellationToken)
    {
        var (jobName, jobPayload, schedule, metadata) = BuildJobPayload(context, transContext);

        // Enqueue to Dapr - external service, TryAsync is appropriate
        var enqueueResult = await EnqueueToDaprAsync(jobName, jobPayload, schedule, metadata, cancellationToken);
        if (!enqueueResult.IsSuccess)
            return Result<string>.Fail(enqueueResult.Error);

        // Save job record - repository call, no Try needed (infrastructure exceptions bubble up)
        await SaveJobRecordAsync(context, transContext, jobName, enqueueResult.Value!, cancellationToken);

        return Result<string>.Ok(jobName);
    }

    /// <summary>
    /// Enqueues the job to Dapr background job service.
    /// Uses TryAsync because Dapr is an external service.
    /// </summary>
    private Task<Result<Guid>> EnqueueToDaprAsync(
        string jobName,
        TransitionJobPayload jobPayload,
        string schedule,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        return ResultExtensions.TryAsync(
            async ct => await backgroundJobService.EnqueueAsync(
                TransitionJobHandler.HandlerName,
                jobName,
                jobPayload,
                schedule,
                metadata,
                ct),
            cancellationToken,
            ex => Error.Dependency(
                WorkflowErrorCodes.Dependency,
                $"Failed to enqueue transition job '{jobName}': {ex.Message}",
                "Dapr"));
    }

    /// <summary>
    /// Saves the job record to the repository.
    /// No Try wrapper - repository exceptions bubble up to middleware.
    /// </summary>
    private Task SaveJobRecordAsync(
        WorkflowExecutionContext context,
        TransitionExecutionContext transContext,
        string jobName,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        return jobRepository.InsertAsync(
            InstanceJob.Create(
                jobId,
                jobName,
                jobId,
                context.Domain,
                context.WorkflowKey,
                transContext.InstanceId),
            true,
            cancellationToken);
    }

    /// <summary>
    /// Builds the job payload, schedule, and metadata.
    /// Pure function - no side effects.
    /// </summary>
    private static (string JobName, TransitionJobPayload Payload, string Schedule, Dictionary<string, object> Metadata)
        BuildJobPayload(WorkflowExecutionContext context, TransitionExecutionContext transContext)
    {
        var jobName = $"trans-{context.InstanceId}-{context.TransitionKey}";

        var jobPayload = new TransitionJobPayload
        {
            JobName = jobName,
            InstanceId = transContext.InstanceId,
            TransitionKey = transContext.TransitionKey,
            Domain = transContext.Domain,
            Workflow = transContext.WorkflowKey,
            Version = transContext.Workflow.Version,
            Data = context.Data?.Attributes,
            InstanceKey = context.Data?.Key,
            Tags = context.Data?.Tags,
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

        return (jobName, jobPayload, schedule, metadata);
    }

    /// <summary>
    /// Logs successful job enqueue.
    /// </summary>
    private void LogEnqueueSuccess(WorkflowExecutionContext context, string jobName)
    {
        logger.TransitionEnqueued(context.TransitionKey, context.InstanceId, jobName);
    }

    /// <summary>
    /// Logs failed job enqueue.
    /// </summary>
    private void LogEnqueueFailure(WorkflowExecutionContext context)
    {
        logger.TransitionEnqueueFailed(context.TransitionKey, context.InstanceId);
    }
}