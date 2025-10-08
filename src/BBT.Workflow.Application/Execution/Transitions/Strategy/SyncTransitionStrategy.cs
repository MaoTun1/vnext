using BBT.Workflow.Execution.Handlers;
using BBT.Workflow.Execution.Pipeline;
using Microsoft.Extensions.Logging;

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
        logger.LogDebug("Executing transition synchronously");

        try
        {
            var handler = handlerFactory.Get(context.TriggerType);
            var ctx = await ctxFactory.CreateAsync(context, cancellationToken); // rehydrate
            await handler.PreHandleAsync(ctx, cancellationToken);
            await pipeline.RunAsync(ctx, cancellationToken);
            await handler.PostHandleAsync(ctx, cancellationToken);
            logger.LogDebug("Synchronous transition execution completed successfully");
            return ctx;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Synchronous transition execution failed");
            throw;
        }
    }
}