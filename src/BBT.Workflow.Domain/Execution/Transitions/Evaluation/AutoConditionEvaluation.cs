using BBT.Aether.Results;

namespace BBT.Workflow.Execution;

/// <summary>
/// Represents the outcome of evaluating an automatic transition's condition.
/// Encapsulates the transition key, evaluation status, and any error that occurred.
/// </summary>
public readonly record struct AutoConditionEvaluation
{
    /// <summary>
    /// Gets the key of the transition that was evaluated.
    /// </summary>
    public string TransitionKey { get; init; }

    /// <summary>
    /// Gets the status of the condition evaluation.
    /// </summary>
    public AutoConditionStatus Status { get; init; }

    /// <summary>
    /// Gets the error that occurred during evaluation, if any.
    /// Only populated when Status is Failed.
    /// </summary>
    public Error? Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether the condition was successfully satisfied.
    /// True only when Status is Satisfied.
    /// </summary>
    public bool IsSuccess => Status == AutoConditionStatus.Satisfied;

    /// <summary>
    /// Gets a value indicating whether the condition evaluation failed with a technical error.
    /// </summary>
    public bool IsFailed => Status == AutoConditionStatus.Failed;

    /// <summary>
    /// Creates a new AutoConditionEvaluation instance.
    /// </summary>
    /// <param name="transitionKey">The key of the evaluated transition.</param>
    /// <param name="status">The evaluation status.</param>
    /// <param name="error">Optional error information for failed evaluations.</param>
    public AutoConditionEvaluation(
        string transitionKey,
        AutoConditionStatus status,
        Error? error = null)
    {
        TransitionKey = transitionKey;
        Status = status;
        Error = error;
    }

    /// <summary>
    /// Creates a successful evaluation result (Satisfied status).
    /// </summary>
    /// <param name="transitionKey">The key of the transition.</param>
    /// <returns>An AutoConditionEvaluation with Satisfied status.</returns>
    public static AutoConditionEvaluation Satisfied(string transitionKey) =>
        new(transitionKey, AutoConditionStatus.Satisfied);

    /// <summary>
    /// Creates an unsatisfied evaluation result (NotSatisfied status).
    /// </summary>
    /// <param name="transitionKey">The key of the transition.</param>
    /// <returns>An AutoConditionEvaluation with NotSatisfied status.</returns>
    public static AutoConditionEvaluation NotSatisfied(string transitionKey) =>
        new(transitionKey, AutoConditionStatus.NotSatisfied);

    /// <summary>
    /// Creates a failed evaluation result (Failed status).
    /// </summary>
    /// <param name="transitionKey">The key of the transition.</param>
    /// <param name="error">The error that caused the failure.</param>
    /// <returns>An AutoConditionEvaluation with Failed status.</returns>
    public static AutoConditionEvaluation Failed(string transitionKey, Error error) =>
        new(transitionKey, AutoConditionStatus.Failed, error);
}

