using BBT.Aether.Results;

namespace BBT.Workflow.Execution;

/// <summary>
/// Centralized factory methods for execution-related domain errors.
/// Provides strongly-typed error creation to reduce string noise and improve code readability.
/// </summary>
/// </remarks>
public static class ExecutionErrors
{
    #region Instance Errors

    /// <summary>
    /// Creates an error when attempting to cancel an already completed instance.
    /// </summary>
    /// <param name="instanceId">The ID of the workflow instance.</param>
    /// <param name="currentStatus">The current status description of the instance.</param>
    public static Error InstanceAlreadyCompleted(Guid instanceId, string currentStatus)
        => Error.Validation(
            WorkflowErrorCodes.InvalidState,
            $"Cannot cancel instance {instanceId}: already in {currentStatus} state",
            target: instanceId.ToString());

    /// <summary>
    /// Creates an error when a workflow instance is not found during transition response build.
    /// </summary>
    /// <param name="instanceId">The ID of the workflow instance.</param>
    public static Error InstanceNotFoundForResponse(string instanceId)
        => Error.NotFound(
            WorkflowErrorCodes.NotFoundInstanceData,
            $"Workflow instance {instanceId} not found during transition response build.",
            target: instanceId);
    
    #endregion

    #region Transition Errors

    /// <summary>
    /// Creates an error when no automatic transition condition is satisfied.
    /// </summary>
    /// <param name="stateKey">The key of the state being evaluated.</param>
    public static Error NoAutoTransitionConditionSatisfied(string stateKey)
        => Error.Validation(
            WorkflowErrorCodes.AutoTransitionConditionNotMet,
            $"No automatic transition condition is satisfied for state '{stateKey}'. " +
            "At least one automatic transition must have a satisfied condition.");

    /// <summary>
    /// Creates an error when an automatic transition has no rule defined.
    /// </summary>
    /// <param name="transitionKey">The key of the transition without a rule.</param>
    public static Error AutoTransitionNoRuleDefined(string transitionKey)
        => Error.Validation(
            WorkflowErrorCodes.ConfigInvalid,
            $"Automatic transition '{transitionKey}' has no rule defined.");

    /// <summary>
    /// Creates an error when automatic transition rule evaluation fails.
    /// </summary>
    /// <param name="transitionKey">The key of the transition that failed.</param>
    /// <param name="errorMessage">The error message from the script execution.</param>
    /// <param name="detail">Additional detail (e.g., exception details).</param>
    public static Error TransitionRuleEvaluationFailed(string transitionKey, string errorMessage, string? detail = null)
        => Error.Failure(
            WorkflowErrorCodes.TransitionRuleFailed,
            $"Automatic transition rule evaluation failed for '{transitionKey}': {errorMessage}",
            detail: detail);

    /// <summary>
    /// Creates an error when transition mapping script execution fails.
    /// </summary>
    /// <param name="errorMessage">The error message from the script execution.</param>
    /// <param name="exceptionType">The type of exception that occurred.</param>
    public static Error TransitionMappingFailed(string errorMessage, string exceptionType)
        => Error.Failure(
            WorkflowErrorCodes.ExecutionStepFailed,
            $"Failed to execute transition mapping: {errorMessage}",
            detail: exceptionType);
    
    #endregion
    
}

