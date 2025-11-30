using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Execution.ReEntry;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that evaluates and executes automatic transitions.
/// Evaluates all automatic transition conditions and dispatches the first satisfied transition.
/// </summary>
public sealed class RunAutomaticTransitionsStep(
    IAutoConditionEvaluator autoConditionEvaluator,
    ILogger<RunAutomaticTransitionsStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Auto;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(RunAutomaticTransitionsStep)}");

        // Check if target state has any automatic transitions
        if (context.Target?.AutoTransitions == null || !context.Target.AutoTransitions.Any())
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway chain: Evaluate all transitions -> Find winner -> Enqueue
        return await EvaluateAllTransitionsAsync(context, cancellationToken)
            .Bind(evaluations => FindWinningTransition(context, evaluations))
            .Tap(winner => EnqueueWinningTransition(context, winner))
            .Map(_ => StepOutcome.Continue());
    }

    /// <summary>
    /// Evaluates all automatic transitions and returns the results.
    /// </summary>
    private async Task<Result<List<AutoConditionEvaluation>>> EvaluateAllTransitionsAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var evaluations = new List<AutoConditionEvaluation>();

        foreach (var automaticTransition in context.Target!.AutoTransitions)
        {
            var evalResult = await autoConditionEvaluator.EvaluateAsync(
                automaticTransition,
                context,
                cancellationToken);

            if (!evalResult.IsSuccess)
            {
                return Result<List<AutoConditionEvaluation>>.Fail(evalResult.Error);
            }

            evaluations.Add(evalResult.Value);
        }

        return Result<List<AutoConditionEvaluation>>.Ok(evaluations);
    }

    /// <summary>
    /// Finds the first satisfied transition from evaluations.
    /// </summary>
    private Result<AutoConditionEvaluation> FindWinningTransition(
        TransitionExecutionContext context,
        List<AutoConditionEvaluation> evaluations)
    {
        var winner = evaluations.FirstOrDefault(e => e.Status == AutoConditionStatus.Satisfied);

        if (winner.TransitionKey is null)
        {
            logger.AutoTransitionConditionNotSatisfied(
                context.Target!.Key,
                context.InstanceId,
                string.Join(", ", evaluations.Select(e => e.TransitionKey)));

            return Result<AutoConditionEvaluation>.Fail(
                ExecutionErrors.NoAutoTransitionConditionSatisfied(context.Target.Key));
        }

        logger.AutoTransitionSelected(winner.TransitionKey, context.Target!.Key, context.InstanceId);

        return Result<AutoConditionEvaluation>.Ok(winner);
    }

    /// <summary>
    /// Enqueues the winning transition for inline execution.
    /// </summary>
    private static void EnqueueWinningTransition(TransitionExecutionContext context, AutoConditionEvaluation winner)
    {
        var command = ReentryCommand.ForAutomatic(
            context.InstanceId,
            context.Domain,
            context.WorkflowKey,
            winner.TransitionKey,
            context.ExecutionChainId,
            context.ChainDepth,
            context.Headers);

        context.Directives.EnqueueInlineAuto(command);
    }
}
