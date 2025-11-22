using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Logging;
using BBT.Workflow.Execution.ReEntry;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that processes inline automatic transition chains.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class ProcessInlineAutoChainStep(IReentryDispatcher dispatcher, IContextRefresher refresher)
    : ITransitionStep
{
    public int Order => LifecycleOrder.AfterEpilogueRefresh;
    private const int MaxInlineHops = 10;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext ctx, CancellationToken ct)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ProcessInlineAutoChainStep)}");

        if (ctx.Directives.InlineAutoQueue.Count == 0) 
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        
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
                var refreshResult = await refresher.RefreshAsync(ctx, ct);
                if (!refreshResult.IsSuccess)
                    return Result<StepOutcome>.Fail(refreshResult.Error);
                
                if (ctx.Instance.IsCompleted)
                {
                    ctx.SkipImmediateExecution = true;
                }
                break; // Stop on first success
            }
        }

        // If we attempted transitions but none succeeded, return error
        if (attemptedCount > 0 && !anySucceeded)
        {
            return Result<StepOutcome>.Fail(WorkflowErrors.AutoTransitionFailed(ctx.InstanceId, ctx.WorkflowKey));
        }

        return Result<StepOutcome>.Ok(StepOutcome.Continue());
    }
}