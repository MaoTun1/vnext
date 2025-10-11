using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.ReEntry;

namespace BBT.Workflow.Execution.Pipeline.Steps;

public sealed class ProcessInlineAutoChainStep(IReentryDispatcher dispatcher, IContextRefresher refresher)
    : ITransitionStep
{
    public int Order => LifecycleOrder.AfterEpilogueRefresh;
    private const int MaxInlineHops = 10;

    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.Directives.InlineAutoQueue.Count == 0) return StepOutcome.Continue();
        
        var hops = 0;
        var anySucceeded = false;
        var attemptedCount = 0;
        
        while (ctx.Directives.InlineAutoQueue.Count > 0 && hops++ < MaxInlineHops)
        {
            var cmd = ctx.Directives.InlineAutoQueue.Dequeue();
            attemptedCount++;
            
            var outcome = await dispatcher.DispatchAutoAsync(cmd with { PreferInline = true }, ct);
            if (!outcome.InlineExecuted) break;

            if (outcome.Succeeded)
            {
                anySucceeded = true;
                // Refresh and check for completion
                await refresher.RefreshAsync(ctx, ct);
                if (ctx.Instance.IsCompleted)
                {
                    ctx.SkipImmediateExecution = true;
                }
                break; // Stop on first success
            }
        }

        // If we attempted transitions but none succeeded, throw exception
        if (attemptedCount > 0 && !anySucceeded)
        {
            throw new AutoTransitionFailedException(ctx.InstanceId, ctx.WorkflowKey);
        }

        return StepOutcome.Continue();
    }
}