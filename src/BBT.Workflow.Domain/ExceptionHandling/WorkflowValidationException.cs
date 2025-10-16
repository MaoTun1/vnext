using System.ComponentModel.DataAnnotations;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when workflow validation fails.
/// This exception bridges the Result pattern with exception-based error handling,
/// providing detailed error information including field-level validation errors.
/// </summary>
public sealed class WorkflowValidationException : Exception
{
    /// <summary>
    /// Gets the error information from Result pattern.
    /// </summary>
    public Domain.Error Error { get; }

    /// <summary>
    /// Gets the error code from the validation error.
    /// </summary>
    public string ErrorCode => Error.Code;

    /// <summary>
    /// Gets the target field or resource that caused the error.
    /// </summary>
    public string? Target => Error.Target;

    /// <summary>
    /// Gets detailed field-level validation errors if available.
    /// </summary>
    public IReadOnlyCollection<ValidationResult>? ValidationErrors => Error.ValidationErrors;

    /// <summary>
    /// Initializes a new instance of WorkflowValidationException from a Result Error.
    /// </summary>
    /// <param name="error">The error from Result pattern</param>
    public WorkflowValidationException(Domain.Error error)
        : base(error.Message ?? error.Code)
    {
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of WorkflowValidationException with an error code and message.
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <param name="message">The error message</param>
    public WorkflowValidationException(string errorCode, string message)
        : base(message)
    {
        Error = Domain.Error.Validation(errorCode, message);
    }

    /// <summary>
    /// Initializes a new instance of WorkflowValidationException with detailed validation errors.
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <param name="message">The error message</param>
    /// <param name="validationErrors">The detailed validation errors</param>
    /// <param name="target">The target field or resource</param>
    public WorkflowValidationException(
        string errorCode,
        string message,
        IReadOnlyCollection<ValidationResult> validationErrors,
        string? target = null)
        : base(message)
    {
        Error = Domain.Error.Validation(errorCode, message, validationErrors, target);
    }
}

