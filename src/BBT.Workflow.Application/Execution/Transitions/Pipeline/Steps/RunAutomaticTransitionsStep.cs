using BBT.Workflow.Execution.ReEntry;
using BBT.Workflow.ExceptionHandling;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that evaluates and executes automatic transitions.
/// Checks conditions and dispatches qualifying automatic transitions for execution.
/// </summary>
public sealed class RunAutomaticTransitionsStep(
    IReentryDispatcher reentryDispatcher,
    ILogger<RunAutomaticTransitionsStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Auto;

    /// <inheritdoc />
    public async Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target?.AutoTransitions == null || !context.Target.AutoTransitions.Any())
        {
            logger.LogTrace("No automatic transitions for state {StateName}", context.Target?.Key ?? "Unknown");
            return;
        }

        logger.LogDebug("Evaluating {Count} automatic transitions for state {StateName} on instance {InstanceId}",
            context.Target.AutoTransitions.Count(), context.Target.Key, context.InstanceId);

        bool anySuccess = false;

        foreach (var automaticTransition in context.Target.AutoTransitions)
        {
            try
            {
                await EvaluateAndDispatchAutomaticTransitionAsync(context, automaticTransition, cancellationToken);
                anySuccess = true;
                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Operation was cancelled, re-throw to propagate cancellation
                throw;
            }
            catch (TransitionRuleFailedException ex)
            {
                logger.LogWarning(ex,
                    "AutoTransition transition rule failed. InstanceId={InstanceId}, Transition={TransitionKey}. Continuing to next transition.",
                    context.InstanceId, automaticTransition.Key);
                // Continue to next transition
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "AutoTransition failed. InstanceId={InstanceId}, Transition={TransitionKey}. Trying next transition.",
                    context.InstanceId, automaticTransition.Key);
                throw;
            }
        }

        if (!anySuccess)
        {
            throw new AutoTransitionFailedException(context.InstanceId, context.WorkflowKey);
        }

        logger.LogDebug("Completed automatic transitions evaluation for state {StateName}", context.Target.Key);
    }

    /// <summary>
    /// Evaluates the condition for an automatic transition and dispatches it if the condition is met.
    /// </summary>
    private async Task EvaluateAndDispatchAutomaticTransitionAsync(
        TransitionExecutionContext context,
        Definitions.Transition automaticTransition,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Evaluating automatic transition {TransitionKey} for instance {InstanceId}",
            automaticTransition.Key, context.InstanceId);

        // Create re-entry command for automatic transition
        var command = ReentryCommand.ForAutomatic(
            context.InstanceId,
            context.Domain,
            context.WorkflowKey,
            automaticTransition.Key,
            context.ExecutionChainId,
            context.ChainDepth,
            context.Headers);

        // Dispatch for execution
        await reentryDispatcher.DispatchAutoAsync(command, cancellationToken);

        logger.LogTrace("Dispatched automatic transition {TransitionKey} for instance {InstanceId}",
            automaticTransition.Key, context.InstanceId);
    }
}
