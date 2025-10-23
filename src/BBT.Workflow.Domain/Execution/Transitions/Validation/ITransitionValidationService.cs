using BBT.Workflow.Domain;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Execution.Validation;

/// <summary>
/// Interface for transition validation operations within the execution pipeline using Result Pattern.
/// This service handles transition validation, rule execution, schema validation, and policy checks without throwing exceptions.
/// </summary>
public interface ITransitionValidationService
{
    /// <summary>
    /// Validates a transition execution context before pipeline execution using Result Pattern.
    /// Returns Result.Ok() if all validations pass, or Result.Fail() with detailed error information on failure.
    /// </summary>
    /// <param name="context">The transition execution context containing all necessary data</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Result indicating validation success or failure with detailed error information</returns>
    /// <remarks>
    /// This method performs validation steps using Result pattern:
    /// 1. Checks if the instance can execute the transition based on policies (returns Result with rule error details)
    /// 2. Validates input data against the transition's schema if present (returns Result with field-level validation errors)
    /// All validations use Result pattern and provide detailed error information for client consumption.
    /// </remarks>
    Task<Result> ValidateAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default);
}
