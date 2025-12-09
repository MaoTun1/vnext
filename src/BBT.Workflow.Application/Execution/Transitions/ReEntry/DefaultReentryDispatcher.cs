using BBT.Aether.DependencyInjection;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Execution.ReEntry;

/// <summary>
/// Default implementation of the re-entry dispatcher.
/// Handles automatic and scheduled transitions by either executing inline or enqueuing as background jobs.
/// </summary>
public sealed class DefaultReentryDispatcher(
    ILazyServiceProvider lazyServiceProvider,
    IOptions<ReentryOptions> options,
    ILogger<DefaultReentryDispatcher> logger) : IReentryDispatcher
{
    private readonly ReentryOptions _options = options.Value;

    private IWorkflowExecutionService ExecutionService =>
        lazyServiceProvider.LazyGetRequiredService<IWorkflowExecutionService>();

    /// <inheritdoc />
    /// <summary>
    /// Dispatches an automatic transition for re-entry.
    /// Flow: Validate Chain Depth → Check Inline Preference → Execute or Defer
    /// </summary>
    public async Task<ReentryOutcome> DispatchAutoAsync(ReentryCommand command, CancellationToken cancellationToken)
    {
        var nextCommand = command with { ChainDepth = command.ChainDepth + 1 };

        // Guard: Chain depth exceeded - return early to prevent infinite loops
        if (IsChainDepthExceeded(nextCommand))
            return ReentryOutcome.NotExecuted();

        // Guard: Inline execution not allowed - defer to background job
        if (!ShouldExecuteInline(nextCommand))
            return ReentryOutcome.Deferred();

        // Execute inline and return outcome
        var succeeded = await ExecuteInNewScopeAsync(nextCommand, cancellationToken);
        return ReentryOutcome.Executed(succeeded);
    }

    /// <summary>
    /// Checks if the chain depth has exceeded the maximum allowed hops.
    /// Logs a warning when the limit is exceeded.
    /// </summary>
    private bool IsChainDepthExceeded(ReentryCommand command)
    {
        if (command.ChainDepth <= _options.MaxAutoHops)
            return false;

        logger.MaxAutoHopsExceeded(_options.MaxAutoHops, command.InstanceId, command.ExecutionChainId);

        return true;
    }

    /// <summary>
    /// Determines if the transition should be executed inline based on command preferences and options.
    /// </summary>
    private bool ShouldExecuteInline(ReentryCommand command)
        => command.PreferInline && _options.AllowInlineAuto;

    /// <summary>
    /// Executes a transition in a new dependency injection scope.
    /// Infrastructure exceptions bubble up to middleware - no try-catch needed.
    /// </summary>
    private async Task<bool> ExecuteInNewScopeAsync(ReentryCommand command, CancellationToken cancellationToken)
    {
        // using var scope = serviceScopeFactory.CreateScope();
        // var executionService = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionService>();

        var input = WorkflowExecutionContext.From(command);
        var result = await ExecutionService.ExecuteTransitionAsync(input, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.InlineExecutionFailed(result.Error.Message, command.InstanceId, command.ExecutionChainId);
        }
        return result.IsSuccess;
    }
}