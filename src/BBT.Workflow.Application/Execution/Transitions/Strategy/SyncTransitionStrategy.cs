using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Handlers;
using BBT.Workflow.Execution.Pipeline;
using BBT.Workflow.Logging;
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
    /// <summary>
    /// Executes transition synchronously.
    /// Railway chain: Resolve Handler → Create Context → Execute Lifecycle
    /// </summary>
    [Log]
    [Trace]
    public Task<Result<TransitionExecutionContext>> ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        return handlerFactory.Get(context.TriggerType)
            .BindAsync(handler => CreateContextAndExecuteAsync(handler, context, activity, cancellationToken));
    }

    /// <summary>
    /// Creates execution context and runs the handler lifecycle.
    /// Handles telemetry enrichment and activity status as side effects.
    /// </summary>
    private async Task<Result<TransitionExecutionContext>> CreateContextAndExecuteAsync(
        ITransitionHandler handler,
        WorkflowExecutionContext context,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var contextResult = await contextFactory.CreateAsync(context, cancellationToken);
        if (!contextResult.IsSuccess)
        {
            SetActivityError(activity, contextResult.Error);
            return contextResult;
        }

        var ctx = contextResult.Value!;
        EnrichTelemetry(activity, ctx, handler, context.TriggerType);

        var lifecycleResult = await ExecuteHandlerLifecycleAsync(handler, ctx, cancellationToken);

        SetActivityStatus(activity, lifecycleResult);

        return lifecycleResult;
    }

    /// <summary>
    /// Executes the handler lifecycle: PreHandle → Pipeline → PostHandle.
    /// No TryAsync - handlers are application layer, infrastructure exceptions bubble up.
    /// </summary>
    private async Task<Result<TransitionExecutionContext>> ExecuteHandlerLifecycleAsync(
        ITransitionHandler handler,
        TransitionExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        // PreHandle - application layer, no Try needed
        await handler.PreHandleAsync(ctx, cancellationToken);

        // Pipeline execution - returns Result
        var pipelineResult = await pipeline.RunAsync(ctx, cancellationToken);
        if (!pipelineResult.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(pipelineResult.Error);

        // PostHandle - application layer, no Try needed
        await handler.PostHandleAsync(ctx, cancellationToken);

        return Result<TransitionExecutionContext>.Ok(ctx);
    }

    /// <summary>
    /// Enriches the activity with telemetry tags.
    /// Pure side effect - doesn't affect flow.
    /// </summary>
    private static void EnrichTelemetry(
        Activity? activity,
        TransitionExecutionContext ctx,
        ITransitionHandler handler,
        TriggerType triggerType)
    {
        if (activity is null) return;

        activity.SetTag(TelemetryConstants.TagNames.Domain, ctx.Workflow.Domain);
        activity.SetTag(TelemetryConstants.TagNames.Flow, ctx.Workflow.Key);
        activity.SetTag(TelemetryConstants.TagNames.FlowVersion, ctx.Workflow.Version);
        activity.SetTag(TelemetryConstants.TagNames.InstanceId, ctx.InstanceId.ToString());
        activity.SetTag(TelemetryConstants.TagNames.TransitionKey, ctx.TransitionKey);
        activity.SetTag(TelemetryConstants.TagNames.TriggerType, triggerType.ToString());
        activity.SetTag(TelemetryConstants.TagNames.HandlerName, handler.GetType().Name);

        activity.SetBaggage(TelemetryConstants.TagNames.Domain, ctx.Workflow.Domain);
        activity.SetBaggage(TelemetryConstants.TagNames.Flow, ctx.Workflow.Key);
        activity.SetBaggage(TelemetryConstants.TagNames.FlowVersion, ctx.Workflow.Version);
        activity.SetBaggage(TelemetryConstants.TagNames.InstanceId, ctx.InstanceId.ToString());
        
        activity.SetDisplayName($"{ctx.InstanceId}/{ctx.TransitionKey}");
    }

    /// <summary>
    /// Sets activity status based on result.
    /// </summary>
    private static void SetActivityStatus<T>(Activity? activity, Result<T> result)
    {
        if (activity is null) return;

        if (result.IsSuccess)
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            SetActivityError(activity, result.Error);
        }
    }

    /// <summary>
    /// Sets activity error status with error details.
    /// </summary>
    private static void SetActivityError(Activity? activity, Error error)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, error.Message);
        activity.AddTag("error.code", error.Code);
    }
}