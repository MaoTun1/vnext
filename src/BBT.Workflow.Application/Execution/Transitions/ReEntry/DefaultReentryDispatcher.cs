using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Execution.ReEntry;

/// <summary>
/// Default implementation of the re-entry dispatcher.
/// Handles automatic and scheduled transitions by either executing inline or enqueuing as background jobs.
/// </summary>
public sealed class DefaultReentryDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ReentryOptions> options,
    ILogger<DefaultReentryDispatcher> logger) : IReentryDispatcher
{
    private readonly ReentryOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ReentryOutcome> DispatchAutoAsync(ReentryCommand command, CancellationToken cancellationToken)
    {
        var nextCommand = command with { ChainDepth = command.ChainDepth + 1 };

        // Check for infinite loop protection
        if (nextCommand.ChainDepth > _options.MaxAutoHops)
        {
            logger.LogWarning(
                "Maximum auto transition hops ({MaxHops}) exceeded for instance {InstanceId}, chain {ExecutionChainId}",
                _options.MaxAutoHops, command.InstanceId, command.ExecutionChainId);
            return new ReentryOutcome(false, false, null, null, null);
        }

        if (!nextCommand.PreferInline || !_options.AllowInlineAuto)
        {
            // The transition can be advanced as a background job.
            return new ReentryOutcome(InlineExecuted: false, Succeeded: false, NewState: null, NextTransitionKey: null,
                ResumeFromOrder: null);
        }
        
        var succeeded = await InvokeInNewScopeAsync(nextCommand, cancellationToken);
        
        return new ReentryOutcome(InlineExecuted: true, Succeeded: succeeded, NewState: null, NextTransitionKey: null, ResumeFromOrder: null);
    }

    /// <summary>
    /// Invokes a transition in a new dependency injection scope.
    /// </summary>
    private async Task<bool> InvokeInNewScopeAsync(ReentryCommand command, CancellationToken cancellationToken)
    {
        logger.LogTrace("Executing inline re-entry for transition {TransitionKey} on instance {InstanceId}",
            command.TransitionKey, command.InstanceId);

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var executionService = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionService>();

            var input = WorkflowExecutionContext.From(command);
            await executionService.ExecuteTransitionAsync(input, cancellationToken);

            logger.LogTrace("Completed inline re-entry for transition {TransitionKey} on instance {InstanceId}",
                command.TransitionKey, command.InstanceId);
            return true; // Success
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Inline re-entry cancelled for transition {TransitionKey} on instance {InstanceId}",
                command.TransitionKey, command.InstanceId);
            throw;
        }
        catch (TransitionRuleFailedException ex)
        {
            logger.LogWarning(ex,
                "Inline re-entry transition rule failed for transition {TransitionKey} on instance {InstanceId}: {Message}",
                command.TransitionKey, command.InstanceId, ex.Message);
            // Don't rethrow for rule failures as they are expected business logic
            return false; // Rule failed, not successful
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to execute inline re-entry for transition {TransitionKey} on instance {InstanceId}",
                command.TransitionKey, command.InstanceId);
            throw;
        }
    }
}