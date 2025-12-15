using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Aether.Uow;
using BBT.Workflow.Execution.ReEntry;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Orchestrates transition chaining with isolated DI scope and UoW per hop.
/// Each transition runs in its own scope with RequiresNew UoW for complete isolation.
/// This ensures deterministic post-commit behavior for inline auto chain processing.
/// </summary>
public sealed class TransitionRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<ReentryOptions> options,
    ILogger<TransitionRunner> logger) : ITransitionRunner
{
    private readonly ReentryOptions _options = options.Value;

    /// <inheritdoc />
    /// <summary>
    /// Runs transitions in a loop, each in its own DI scope + RequiresNew UoW.
    /// Post-commit: enqueues inline auto chain transitions from DirectivesSnapshot.
    /// </summary>
    [Trace]
    public async Task<Result<TransitionOutput>> RunAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var queue = new Queue<WorkflowExecutionContext>();
        queue.Enqueue(context);

        var hop = 0;
        TransitionOutput? lastOutput = null;

        while (queue.TryDequeue(out var current))
        {
            hop++;

            // Guard: prevent infinite loops
            if (hop > _options.MaxAutoHops)
            {
                if (Guid.TryParse(current.InstanceId, out var instanceGuid))
                {
                    logger.MaxAutoHopsExceeded(_options.MaxAutoHops, instanceGuid, current.Execution?.ExecutionChainId);
                }
                return Result<TransitionOutput>.Fail(
                    Error.Validation("auto.chain.maxhops", $"Auto chain exceeded max hops: {_options.MaxAutoHops}"));
            }

            // Execute in isolated scope + UoW
            var hopResult = await ExecuteHopAsync(current, cancellationToken);
            if (!hopResult.IsSuccess)
                return Result<TransitionOutput>.Fail(hopResult.Error);

            var coreOutput = hopResult.Value!;
            lastOutput = coreOutput.Output;

            // POST-COMMIT: enqueue inline auto chain transitions
            if (coreOutput.DirectivesSnapshot.HasQueuedTransitions)
            {
                foreach (var cmd in coreOutput.DirectivesSnapshot.InlineAutoQueue)
                {
                    // Increment chain depth for the next hop
                    var nextCmd = cmd with { ChainDepth = cmd.ChainDepth + 1 };
                    
                    // Guard: check chain depth before enqueueing
                    if (nextCmd.ChainDepth <= _options.MaxAutoHops)
                    {
                        queue.Enqueue(WorkflowExecutionContext.From(nextCmd));
                    }
                    else
                    {
                        logger.MaxAutoHopsExceeded(_options.MaxAutoHops, nextCmd.InstanceId, nextCmd.ExecutionChainId);
                    }
                }
            }
        }

        return lastOutput is null
            ? Result<TransitionOutput>.Fail(Error.Failure("transition.output.missing", "Transition output missing"))
            : Result<TransitionOutput>.Ok(lastOutput);
    }

    /// <summary>
    /// Executes a single transition hop in a new DI scope with RequiresNew UoW.
    /// This ensures complete isolation from any ambient UoW.
    /// </summary>
    private async Task<Result<TransitionCoreOutput>> ExecuteHopAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var core = sp.GetRequiredService<IWorkflowExecutionCore>();

        await using var uow = await uowManager.BeginAsync(
            new UnitOfWorkOptions { Scope = UnitOfWorkScopeOption.RequiresNew },
            cancellationToken);

        var coreResult = await core.ExecuteTransitionCoreAsync(context, cancellationToken);
        if (!coreResult.IsSuccess)
            return Result<TransitionCoreOutput>.Fail(coreResult.Error);

        // Commit is THE boundary - post-commit processing happens after this
        await uow.CommitAsync(cancellationToken);

        return coreResult;
    }
}

