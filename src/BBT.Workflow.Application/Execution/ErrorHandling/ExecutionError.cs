using BBT.Aether.Results;

namespace BBT.Workflow.Execution.ErrorHandling;

/// <summary>
/// Represents an error from task execution with full context for Error Boundary resolution.
/// Carries the normalized error information along with task-specific details
/// for policy resolution and error handling.
/// </summary>
public sealed record ExecutionError
{
    /// <summary>
    /// Gets the key of the task that failed.
    /// </summary>
    public required string TaskKey { get; init; }

    /// <summary>
    /// Gets the type of task that failed (e.g., "Http", "Dapr", "Script").
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// Gets the HTTP status code if applicable.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Gets the error message from task execution.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the normalized error for Error Boundary policy matching.
    /// </summary>
    public required NormalizedError NormalizedError { get; init; }

    /// <summary>
    /// Gets the execution duration in milliseconds.
    /// </summary>
    public long ExecutionDurationMs { get; init; }

    /// <summary>
    /// Gets additional metadata from the task execution.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Converts this ExecutionError to an Error for Result pattern propagation.
    /// The error code format preserves both StatusCode and ExceptionType for Error Boundary matching:
    /// - Task:Type:StatusCode (when only status code is available)
    /// - Task:Type:ExceptionType (when only exception type is available)
    /// - Task:Type:StatusCode:ExceptionType (when both are available)
    /// </summary>
    /// <returns>An Error representing this task execution failure.</returns>
    public Error ToError()
    {
        var code = $"Task:{TaskType}:{TaskKey}";

        if (StatusCode.HasValue)
        {
            code = $"{code}:{StatusCode.Value}";
        }

        // Append ExceptionType if available (from NormalizedError or Metadata)
        var exceptionType = NormalizedError.ExceptionType
            ?? (Metadata?.TryGetValue("ExceptionType", out var et) == true ? et.ToString() : null);

        if (!string.IsNullOrEmpty(exceptionType))
        {
            code = $"{code}:{exceptionType}";
        }

        return Error.Failure(code, ErrorMessage ?? "Task execution failed");
    }

    /// <summary>
    /// Creates an ExecutionError from an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="taskKey">The key of the failed task.</param>
    /// <param name="taskType">The type of task.</param>
    /// <param name="executionDurationMs">The execution duration before failure.</param>
    /// <returns>A new ExecutionError instance.</returns>
    public static ExecutionError FromException(
        Exception exception,
        string taskKey,
        string taskType,
        long executionDurationMs = 0)
    {
        return new ExecutionError
        {
            TaskKey = taskKey,
            TaskType = taskType,
            StatusCode = null,
            ErrorMessage = exception.Message,
            NormalizedError = new NormalizedError
            {
                Code = $"Task:{taskType}:Exception",
                Layer = ErrorLayer.Task,
                ExceptionType = exception.GetType().Name,
                StatusCode = null,
                Message = exception.Message,
                Source = ErrorSource.Exception,
                IsTransient = IsTransientException(exception)
            },
            ExecutionDurationMs = executionDurationMs,
            Metadata = new Dictionary<string, object>
            {
                ["ExceptionType"] = exception.GetType().Name,
                ["StackTrace"] = exception.StackTrace ?? string.Empty
            }
        };
    }

    /// <summary>
    /// Creates an ExecutionError from an Error.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <param name="taskKey">The key of the failed task.</param>
    /// <param name="taskType">The type of task.</param>
    /// <param name="executionDurationMs">The execution duration.</param>
    /// <returns>A new ExecutionError instance.</returns>
    public static ExecutionError FromError(
        Error error,
        string taskKey,
        string taskType,
        long executionDurationMs = 0)
    {
        return new ExecutionError
        {
            TaskKey = taskKey,
            TaskType = taskType,
            StatusCode = null,
            ErrorMessage = error.Message,
            NormalizedError = new NormalizedError
            {
                Code = $"Task:{taskType}:{taskKey}",
                Layer = ErrorLayer.Task,
                ExceptionType = null,
                StatusCode = null,
                Message = error.Message ?? "Task execution failed",
                Source = ErrorSource.ResultFailure,
                IsTransient = false,
                OriginalCode = error.Code
            },
            ExecutionDurationMs = executionDurationMs
        };
    }

    /// <summary>
    /// Determines if an exception is typically transient.
    /// </summary>
    private static bool IsTransientException(Exception exception)
    {
        return exception is TimeoutException
            or TaskCanceledException
            or HttpRequestException
            or OperationCanceledException;
    }
}

