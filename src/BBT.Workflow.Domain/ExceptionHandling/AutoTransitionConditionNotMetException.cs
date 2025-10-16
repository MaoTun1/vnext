namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when an automatic transition condition is not met.
/// This is a special exception used internally to handle multi-auto-transition scenarios
/// where some transitions may fail condition checks while others succeed.
/// </summary>
public sealed class AutoTransitionConditionNotMetException : Exception
{
    /// <summary>
    /// The error code associated with this exception.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the AutoTransitionConditionNotMetException class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The exception message.</param>
    public AutoTransitionConditionNotMetException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the AutoTransitionConditionNotMetException class with an inner exception.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AutoTransitionConditionNotMetException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

