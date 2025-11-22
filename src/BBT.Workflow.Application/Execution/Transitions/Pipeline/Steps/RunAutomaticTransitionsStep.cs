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
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class RunAutomaticTransitionsStep : ITransitionStep
{
    private readonly IAutoConditionEvaluator _autoConditionEvaluator;
    private readonly ILogger<RunAutomaticTransitionsStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunAutomaticTransitionsStep"/> class.
    /// </summary>
    /// <param name="autoConditionEvaluator">Service for evaluating automatic transition conditions.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public RunAutomaticTransitionsStep(
        IAutoConditionEvaluator autoConditionEvaluator,
        ILogger<RunAutomaticTransitionsStep> logger)
    {
        _autoConditionEvaluator = autoConditionEvaluator ?? throw new ArgumentNullException(nameof(autoConditionEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
        
        var evaluations = new List<AutoConditionEvaluation>();

        // Evaluate all automatic transitions
        foreach (var automaticTransition in context.Target.AutoTransitions)
        {
            var evalResult = await _autoConditionEvaluator.EvaluateAsync(
                automaticTransition,
                context,
                cancellationToken);

            if (!evalResult.IsSuccess)
            {
                return Result<StepOutcome>.Fail(evalResult.Error);
            }

            evaluations.Add(evalResult.Value);
        }

        // Find the first satisfied transition
        var winner = evaluations.FirstOrDefault(e => e.Status == AutoConditionStatus.Satisfied);

        if (winner.TransitionKey is null)
        {
            // All conditions were NotSatisfied - this is a business rule violation
            _logger.LogWarning(
                "No automatic transition condition is satisfied for current state. " +
                "StateKey={StateKey}, InstanceId={InstanceId}, EvaluatedTransitions={TransitionKeys}",
                context.Target.Key,
                context.InstanceId,
                string.Join(", ", evaluations.Select(e => e.TransitionKey)));

            return Result<StepOutcome>.Fail(
                Error.Validation(
                    WorkflowErrorCodes.AutoTransitionConditionNotMet,
                    $"No automatic transition condition is satisfied for state '{context.Target.Key}'. " +
                    $"At least one automatic transition must have a satisfied condition."));
        }

        _logger.LogInformation(
            "Automatic transition selected for execution. TransitionKey={TransitionKey}, " +
            "StateKey={StateKey}, InstanceId={InstanceId}",
            winner.TransitionKey, context.Target.Key, context.InstanceId);

        // Enqueue the winning transition for inline execution
        var command = ReentryCommand.ForAutomatic(
            context.InstanceId,
            context.Domain,
            context.WorkflowKey,
            winner.TransitionKey,
            context.ExecutionChainId,
            context.ChainDepth,
            context.Headers);

        context.Directives.EnqueueInlineAuto(command);

        return Result<StepOutcome>.Ok(StepOutcome.Continue());
    }
}
