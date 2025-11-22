using BBT.Workflow.Execution.Handlers;
using BBT.Workflow.Execution.Pipeline;
using BBT.Workflow.Logging;
using BBT.Workflow.Definitions;
using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Synchronous transition execution strategy.
/// Executes transitions immediately in the current thread/context.
/// </summary>
public sealed class SyncTransitionStrategy(
    ITransitionHandlerFactory handlerFactory,
    ITransitionContextFactory contextFactory,
    TransitionPipeline pipeline) : ITransitionStrategy
{
    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<TransitionExecutionContext>> ExecuteAsync(WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        // Railway: Resolve handler -> Create context -> Execute lifecycle
        var handlerResult = handlerFactory.Get(context.TriggerType);
        if (!handlerResult.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(handlerResult.Error);

        var handler = handlerResult.Value!;

        var contextResult = await contextFactory.CreateAsync(context, cancellationToken);
        if (!contextResult.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, contextResult.Error.Message);
            activity?.AddTag("error.code", contextResult.Error.Code);
            return Result<TransitionExecutionContext>.Fail(contextResult.Error);
        }

        var ctx = contextResult.Value!;
        await EnrichTelemetry(activity, ctx, handler, context.TriggerType);

        var lifecycleResult = await ExecuteHandlerLifecycleAsync(handler, ctx, cancellationToken);
        
        if (lifecycleResult.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Error, lifecycleResult.Error.Message);
            activity?.AddTag("error.code", lifecycleResult.Error.Code);
        }

        return lifecycleResult;
    }

    private static Task EnrichTelemetry(
        Activity? activity,
        TransitionExecutionContext ctx,
        ITransitionHandler handler,
        TriggerType triggerType)
    {
        if (activity is null) return Task.CompletedTask;
        
        activity.SetTag(TelemetryConstants.TagNames.Domain, ctx.Workflow.Domain);
        activity.SetTag(TelemetryConstants.TagNames.Flow, ctx.Workflow.Key);
        activity.SetTag(TelemetryConstants.TagNames.FlowVersion, ctx.Workflow.Version);
        activity.SetTag(TelemetryConstants.TagNames.InstanceId, ctx.InstanceId.ToString());
        activity.SetTag(TelemetryConstants.TagNames.TransitionKey, ctx.TransitionKey);
        activity.SetTag(TelemetryConstants.TagNames.TriggerType, triggerType.ToString());
        activity.SetTag(TelemetryConstants.TagNames.HandlerName, handler.GetType().Name);
        activity.SetDisplayName($"{ctx.InstanceId}/{ctx.TransitionKey}");

        return Task.CompletedTask;
    }

    private async Task<Result<TransitionExecutionContext>> ExecuteHandlerLifecycleAsync(
        ITransitionHandler handler,
        TransitionExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        // Railway: PreHandle -> Pipeline -> PostHandle with exception handling
        var preHandleResult = await ResultExtensions.TryAsync(
            async ct => await handler.PreHandleAsync(ctx, ct),
            cancellationToken);
        
        if (!preHandleResult.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(preHandleResult.Error);

        var pipelineResult = await pipeline.RunAsync(ctx, cancellationToken);
        if (!pipelineResult.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(pipelineResult.Error);

        var postHandleResult = await ResultExtensions.TryAsync(
            async ct => await handler.PostHandleAsync(ctx, ct),
            cancellationToken);
        
        if (!postHandleResult.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(postHandleResult.Error);

        return Result<TransitionExecutionContext>.Ok(ctx);
    }
}