using BBT.Workflow.Domain;
using BBT.Workflow.Execution.Handlers;
using BBT.Workflow.Execution.Pipeline;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Synchronous transition execution strategy.
/// Executes transitions immediately in the current thread/context.
/// </summary>
public sealed class SyncTransitionStrategy(
    ITransitionHandlerFactory handlerFactory,
    ITransitionContextFactory ctxFactory,
    TransitionPipeline pipeline,
    ILogger<SyncTransitionStrategy> logger) : ITransitionStrategy
{
    /// <inheritdoc />
    public async Task<Result<TransitionExecutionContext>> ExecuteAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        
        logger.StrategyExecutionStarted(
            TelemetryConstants.Prefixes.Execution,
            nameof(SyncTransitionStrategy),
            context.TransitionKey,
            context.InstanceId);

        // 1. Resolve handler
        var handler = handlerFactory.Get(context.TriggerType);
        
        // 2. Create execution context (rehydrate)
        var ctxResult = await ctxFactory.CreateAsync(context, cancellationToken);
        if (!ctxResult.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(ctxResult.Error);
        
        var ctx = ctxResult.Value!;
        
        logger.ContextCreated(
            TelemetryConstants.Prefixes.Execution,
            context.TransitionKey,
            handler.GetType().Name);

        // 2. Create structured logging scope for entire transition execution
        using var scope = logger.ForTransition(
            ctx.Workflow.Domain,
            ctx.Workflow.Key,
            ctx.Workflow.Version?.ToString(),
            ctx.InstanceId,
            ctx.TransitionKey);

        // 3. Create distributed tracing span for transition execution
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            TelemetryConstants.SpanNames.TransitionExecution,
            ActivityKind.Internal);
        
        activity?.SetTag(TelemetryConstants.TagNames.Domain, ctx.Workflow.Domain);
        activity?.SetTag(TelemetryConstants.TagNames.Flow, ctx.Workflow.Key);
        activity?.SetTag(TelemetryConstants.TagNames.FlowVersion, ctx.Workflow.Version?.ToString());
        activity?.SetTag(TelemetryConstants.TagNames.InstanceId, ctx.InstanceId.ToString());
        activity?.SetTag(TelemetryConstants.TagNames.TransitionKey, ctx.TransitionKey);
        activity?.SetTag(TelemetryConstants.TagNames.TriggerType, context.TriggerType.ToString());
        activity?.SetTag(TelemetryConstants.TagNames.HandlerName, handler.GetType().Name);
        
        // Set display name for better trace visualization
        activity?.SetDisplayName($"{ctx.InstanceId}/{ctx.TransitionKey}");

        // 4. Execute handler lifecycle: PreHandle -> Pipeline -> PostHandle
        // Pre-handle phase
        await handler.PreHandleAsync(ctx, cancellationToken);
        
        // Pipeline execution (now returns Result)
        var pipelineResult = await pipeline.RunAsync(ctx, cancellationToken);
        if (!pipelineResult.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, pipelineResult.Error.Message);
            activity?.AddTag("error.code", pipelineResult.Error.Code);
            
            logger.TransitionFailed(new Exception(pipelineResult.Error.Message ?? pipelineResult.Error.Code), 
                TelemetryConstants.Prefixes.Execution, context.TransitionKey, context.InstanceId);
            
            sw.Stop();
            return Result<TransitionExecutionContext>.Fail(pipelineResult.Error);
        }
        
        // Post-handle phase
        await handler.PostHandleAsync(ctx, cancellationToken);
        
        var executionResult = Result<TransitionExecutionContext>.Ok(ctx);
        
        sw.Stop();
        
        if (!executionResult.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, executionResult.Error.Message);
            activity?.AddTag("error.code", executionResult.Error.Code);
            
            logger.TransitionFailed(new Exception(executionResult.Error.Message ?? executionResult.Error.Code), 
                TelemetryConstants.Prefixes.Execution, context.TransitionKey, context.InstanceId);
            
            return executionResult;
        }
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        logger.StrategyExecutionCompleted(
            TelemetryConstants.Prefixes.Execution,
            nameof(SyncTransitionStrategy),
            context.TransitionKey,
            sw.ElapsedMilliseconds);
        
        return executionResult;
    }
}