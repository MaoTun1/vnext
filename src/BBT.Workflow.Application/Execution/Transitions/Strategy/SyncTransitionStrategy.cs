using BBT.Workflow.ExceptionHandling;
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
    public async Task<TransitionExecutionContext> ExecuteAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        
        logger.StrategyExecutionStarted(
            TelemetryConstants.Prefixes.Execution,
            nameof(SyncTransitionStrategy),
            context.TransitionKey,
            context.InstanceId);

        // 1. Resolve handler and create execution context
        var handler = handlerFactory.Get(context.TriggerType);
        var ctx = await ctxFactory.CreateAsync(context, cancellationToken); // rehydrate
        
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

        try
        {
            // 3. Execute handler lifecycle: PreHandle -> Pipeline -> PostHandle
            await handler.PreHandleAsync(ctx, cancellationToken);
            await pipeline.RunAsync(ctx, cancellationToken);
            await handler.PostHandleAsync(ctx, cancellationToken);
            
            sw.Stop();
            logger.StrategyExecutionCompleted(
                TelemetryConstants.Prefixes.Execution,
                nameof(SyncTransitionStrategy),
                context.TransitionKey,
                sw.ElapsedMilliseconds);
            
            return ctx;
        }
        catch (TransitionRuleFailedException ex)
        {
            sw.Stop();
            logger.TransitionRuleFailed(
                TelemetryConstants.Prefixes.Execution,
                context.TransitionKey,
                context.InstanceId,
                ex.Message);
            throw; // Rethrow the exception so DefaultReentryDispatcher can catch it
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.TransitionFailed(ex, TelemetryConstants.Prefixes.Execution, context.TransitionKey, context.InstanceId);
            throw;
        }
    }
}