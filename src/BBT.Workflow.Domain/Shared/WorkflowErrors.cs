using BBT.Workflow.Definitions;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Domain;

/// <summary>
/// Domain-specific error factory for workflow operations.
/// Provides strongly-typed error creation with workflow-specific error codes.
/// </summary>
public static class WorkflowErrors
{
    #region Instance Errors

    /// <summary>
    /// Instance was not found or is in an invalid state.
    /// </summary>
    public static Error InstanceNotFound(Guid instanceId, string reason)
        => Error.NotFound(
            WorkflowErrorCodes.NotFoundInitialState,
            $"Instance \"{instanceId}\" {reason}",
            target: instanceId.ToString());

    /// <summary>
    /// Instance already exists with the given key.
    /// </summary>
    public static Error InstanceAlreadyExists(string instanceKey)
        => Error.Conflict(
            "instanceExists",
            $"An active instance with key \"{instanceKey}\" already exists",
            target: instanceKey);

    /// <summary>
    /// Instance is already completed and cannot be modified.
    /// </summary>
    public static Error InstanceCompleted(Guid instanceId)
        => Error.Validation(
            "instanceCompleted",
            $"Instance \"{instanceId}\" is already completed",
            target: instanceId.ToString());

    /// <summary>
    /// Instance transition is blocked by active SubFlow instances.
    /// </summary>
    public static Error SubFlowBlocked(Guid instanceId, string transitionKey, int activeSubFlowCount)
        => Error.Conflict(
            WorkflowErrorCodes.SubFlowBlocked,
            $"Cannot execute transition \"{transitionKey}\" for instance \"{instanceId}\". " +
            $"There {(activeSubFlowCount == 1 ? "is" : "are")} {activeSubFlowCount} active blocking SubFlow instance{(activeSubFlowCount == 1 ? "" : "s")} that must complete first.",
            target: transitionKey);

    /// <summary>
    /// Instance transition is already in progress (locked).
    /// </summary>
    public static Error TransitionLocked(Guid instanceId, string transitionKey)
        => Error.Conflict(
            WorkflowErrorCodes.TransitionLocked,
            $"A transition is already in progress for instance \"{instanceId}\". " +
            $"Cannot execute transition \"{transitionKey}\" until the current transition completes.",
            target: instanceId.ToString());

    #endregion

    #region Workflow Definition Errors

    /// <summary>
    /// Workflow definition was not found.
    /// </summary>
    public static Error WorkflowNotFound(string workflowKey, string? version = null)
        => Error.NotFound(
            "workflowNotFound",
            $"Workflow \"{workflowKey}\" " + (version != null ? $"version \"{version}\" " : "") + "not found",
            target: workflowKey);

    /// <summary>
    /// Workflow state was not found.
    /// </summary>
    public static Error StateNotFound(string flow, string state)
        => Error.NotFound(
            WorkflowErrorCodes.NotFoundInitialState,
            $"No {state} state found for workflow \"{flow}\"",
            target: state);

    /// <summary>
    /// Workflow state is invalid for the operation.
    /// </summary>
    public static Error InvalidState(string transition, string? fromState = "N/A", string? currentState = "N/A")
        => Error.Validation(
            WorkflowErrorCodes.InvalidState,
            $"Transition \"{transition}\" is not valid for the current state. Expected state: {fromState}, Current state: {currentState}",
            target: transition);

    #endregion

    #region Transition Errors

    /// <summary>
    /// Transition was not found.
    /// </summary>
    public static Error TransitionNotFound(string transitionKey)
        => Error.NotFound(
            WorkflowErrorCodes.NotFoundTransition,
            $"Transition \"{transitionKey}\" not found",
            target: transitionKey);

    /// <summary>
    /// Transition rule evaluation failed.
    /// </summary>
    public static Error TransitionRuleFailed(string transitionKey, string reason)
        => Error.Validation(
            WorkflowErrorCodes.TransitionRuleFailed,
            $"Transition \"{transitionKey}\" rule evaluation failed: {reason}",
            target: transitionKey);

    /// <summary>
    /// Transition schema validation failed with detailed field-level errors.
    /// </summary>
    public static Error SchemaValidationFailed(
        string transitionKey, 
        IReadOnlyCollection<System.ComponentModel.DataAnnotations.ValidationResult> validationErrors)
        => Error.Validation(
            "schemaValidation",
            $"Transition \"{transitionKey}\" schema validation failed",
            validationErrors,
            target: transitionKey);

    /// <summary>
    /// Transition is not authorized for the execution context.
    /// </summary>
    public static Error TransitionUnauthorized(string transitionKey, TriggerType triggerType, ExecutionActor executionActor)
        => Error.Forbidden(
            WorkflowErrorCodes.UnauthorizedTransition,
            $"Transition '{transitionKey}' with trigger type '{triggerType}' cannot be executed by '{executionActor}' context");

    /// <summary>
    /// No automatic transition succeeded.
    /// </summary>
    public static Error AutoTransitionFailed(Guid instanceId, string workflow)
        => Error.Validation(
            WorkflowErrorCodes.AutoTransitionFailed,
            $"No automatic transition succeeded for InstanceId={instanceId} in Workflow={workflow}",
            target: instanceId.ToString());

    /// <summary>
    /// Automatic transition condition was not met.
    /// This is not an error in multi-auto-transition scenarios, just means this specific transition cannot proceed.
    /// </summary>
    public static Error AutoTransitionConditionNotMet(string transitionKey, string? reason = null)
        => Error.Validation(
            WorkflowErrorCodes.AutoTransitionConditionNotMet,
            $"Auto-transition \"{transitionKey}\" condition not met" + (reason != null ? $": {reason}" : ""),
            target: transitionKey);

    #endregion

    #region Configuration Errors

    /// <summary>
    /// SubFlow configuration is invalid or not found.
    /// </summary>
    public static Error ConfigInvalid(Guid instanceId)
        => Error.Validation(
            WorkflowErrorCodes.ConfigInvalid,
            $"SubFlow configuration not found for parent instance {instanceId}",
            target: instanceId.ToString());

    /// <summary>
    /// Runtime schema is in an invalid state.
    /// </summary>
    public static Error RuntimeSchemaInvalid()
        => Error.Validation(
            WorkflowErrorCodes.RuntimeSchemaInvalidState,
            "Only defined system flows can be published");

    /// <summary>
    /// Domain mismatch error.
    /// </summary>
    public static Error DomainNotFound(string requestedDomain, string expectedDomain)
        => Error.NotFound(
            WorkflowErrorCodes.NotFoundDomain,
            $"Invalid domain: \"{requestedDomain}\". Expected domain is \"{expectedDomain}\".",
            target: requestedDomain);

    #endregion

    #region General Errors

    /// <summary>
    /// Generic conflict error (e.g., duplicate record).
    /// </summary>
    public static Error Conflict(string? message = null)
        => Error.Conflict(
            WorkflowErrorCodes.ConflictWorkflow,
            message ?? "A record with the same version already exists.");

    #endregion
}

