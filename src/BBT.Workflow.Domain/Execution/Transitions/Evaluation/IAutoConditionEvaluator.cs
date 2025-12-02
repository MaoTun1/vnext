using BBT.Aether.Results;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Execution;

/// <summary>
/// Service for evaluating automatic transition conditions.
/// Determines whether an automatic transition should be executed based on its condition rule.
/// </summary>
public interface IAutoConditionEvaluator
{
    /// <summary>
    /// Evaluates the condition rule of an automatic transition.
    /// Returns a Result containing the evaluation outcome, distinguishing between:
    /// - Satisfied: Condition is true, transition can execute
    /// - NotSatisfied: Condition is false (normal business outcome, not an error)
    /// - Failed: Technical error during evaluation (script error, misconfiguration, etc.)
    /// </summary>
    /// <param name="transition">The automatic transition to evaluate.</param>
    /// <param name="context">The execution context containing instance data and workflow state.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A Result containing AutoConditionEvaluation.
    /// - Success with Satisfied/NotSatisfied status for normal evaluation outcomes.
    /// - Failure for technical errors during evaluation.
    /// </returns>
    Task<Result<AutoConditionEvaluation>> EvaluateAsync(
        Transition transition,
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default);
}

