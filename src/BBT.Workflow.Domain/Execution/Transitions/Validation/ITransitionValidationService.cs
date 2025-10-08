using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Execution.Validation;

/// <summary>
/// Interface for transition validation operations within the execution pipeline.
/// This service handles transition validation, rule execution, schema validation, and policy checks.
/// </summary>
public interface ITransitionValidationService
{
    /// <summary>
    /// Validates a transition execution context before pipeline execution.
    /// </summary>
    /// <param name="context">The transition execution context containing all necessary data</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous validation operation</returns>
    /// <exception cref="TransitionRuleFailedException">
    /// Thrown when the transition rule evaluation fails or returns false.
    /// </exception>
    /// <exception cref="ValidationException">
    /// Thrown when the provided data does not conform to the transition's JSON schema.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the instance cannot execute the transition based on policies.
    /// </exception>
    /// <remarks>
    /// This method performs several validation steps:
    /// 1. Checks if the instance can execute the transition based on policies
    /// 2. Evaluates any business rules associated with the transition
    /// 3. Validates input data against the transition's schema if present
    /// </remarks>
    Task ValidateAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default);
}
