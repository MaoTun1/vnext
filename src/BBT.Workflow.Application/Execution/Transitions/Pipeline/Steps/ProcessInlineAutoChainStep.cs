using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Logging;
using BBT.Workflow.Execution.ReEntry;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that processes inline automatic transition chains.
/// Uses Result pattern for exception-free error handling.
/// Note: This step contains stateful loop logic that doesn't naturally fit pure Railway pattern.
/// </summary>
public sealed class ProcessInlineAutoChainStep(
    IReentryDispatcher dispatcher, 
    IContextRefresher refresher) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.AfterEpilogueRefresh;
    
    private const int MaxInlineHops = 10;

    /// <inheritdoc />
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext ctx, CancellationToken ct)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ProcessInlineAutoChainStep)}");

        // Early return if no inline auto transitions queued
        if (!HasQueuedTransitions(ctx))
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Process inline auto chain and evaluate result
        var chainResult = await ProcessChainAsync(ctx, ct);
        
        return chainResult.Match(
            onSuccess: _ => Result<StepOutcome>.Ok(StepOutcome.Continue()),
            onFailure: error => Result<StepOutcome>.Fail(error));
    }

    /// <summary>
    /// Checks if there are queued transitions.
    /// </summary>
    private static bool HasQueuedTransitions(TransitionExecutionContext ctx)
        => ctx.Directives.InlineAutoQueue.Count > 0;

    /// <summary>
    /// Processes the inline auto chain with hop limit.
    /// Returns Result indicating success/failure of the chain processing.
    /// </summary>
    private async Task<Result<ChainProcessingResult>> ProcessChainAsync(
        TransitionExecutionContext ctx,
        CancellationToken ct)
    {
        var state = new ChainProcessingState();
        
        while (ctx.Directives.InlineAutoQueue.Count > 0 && state.Hops++ < MaxInlineHops)
        {
            var cmd = ctx.Directives.InlineAutoQueue.Dequeue();
            state.AttemptedCount++;
            
            var dispatchResult = await DispatchAndProcessAsync(ctx, cmd, ct);
            
            if (!dispatchResult.IsSuccess)
            {
                return Result<ChainProcessingResult>.Fail(dispatchResult.Error);
            }

            var outcome = dispatchResult.Value!;
            
            // Stop if not executed inline
            if (!outcome.InlineExecuted) 
                break;

            // Handle success case
            if (outcome.Succeeded)
            {
                state.AnySucceeded = true;
                
                if (ctx.Instance.IsCompleted)
                {
                    ctx.SkipImmediateExecution = true;
                }
                break; // Stop on first success
            }
        }

        // Validate chain processing result
        if (state.AttemptedCount > 0 && !state.AnySucceeded)
        {
            return Result<ChainProcessingResult>.Fail(
                WorkflowErrors.AutoTransitionFailed(ctx.InstanceId, ctx.WorkflowKey));
        }

        return Result<ChainProcessingResult>.Ok(new ChainProcessingResult(state.AnySucceeded));
    }

    /// <summary>
    /// Dispatches command and processes the outcome.
    /// </summary>
    private async Task<Result<ReentryOutcome>> DispatchAndProcessAsync(
        TransitionExecutionContext ctx,
        ReentryCommand cmd,
        CancellationToken ct)
    {
        var outcome = await dispatcher.DispatchAutoAsync(cmd with { PreferInline = true }, ct);
        
        if (!outcome.InlineExecuted || !outcome.Succeeded)
        {
            return Result<ReentryOutcome>.Ok(outcome);
        }

        // Refresh context on success
        var refreshResult = await refresher.RefreshAsync(ctx, ct);
        
        return refreshResult.IsSuccess
            ? Result<ReentryOutcome>.Ok(outcome)
            : Result<ReentryOutcome>.Fail(refreshResult.Error);
    }

    /// <summary>
    /// Tracks chain processing state.
    /// </summary>
    private sealed class ChainProcessingState
    {
        public int Hops { get; set; }
        public int AttemptedCount { get; set; }
        public bool AnySucceeded { get; set; }
    }

    /// <summary>
    /// Represents chain processing result.
    /// </summary>
    private sealed record ChainProcessingResult(bool AnySucceeded);
}
