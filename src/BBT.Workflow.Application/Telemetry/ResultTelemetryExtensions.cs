using System.Diagnostics;
using BBT.Workflow.Domain;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Application.Telemetry;

/// <summary>
/// Provides telemetry integration for Result types with OpenTelemetry and logging.
/// Enables throw-free observability by tracking failures through structured data.
/// </summary>
public static class ResultTelemetryExtensions
{
    /// <summary>
    /// Traces a Result operation with OpenTelemetry Activity and logs failures.
    /// Sets Activity status and tags based on result, and logs with appropriate level.
    /// </summary>
    /// <param name="result">The result to trace</param>
    /// <param name="activity">The OpenTelemetry activity (typically Activity.Current)</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="operationName">Name of the operation being traced</param>
    /// <returns>The original result for chaining</returns>
    public static Result Trace(
        this Result result, 
        Activity? activity, 
        ILogger logger, 
        string operationName)
    {
        if (result.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        TraceError(activity, logger, operationName, result.Error);
        return result;
    }

    /// <summary>
    /// Traces a Result&lt;T&gt; operation with OpenTelemetry Activity and logs failures.
    /// </summary>
    public static Result<T> Trace<T>(
        this Result<T> result, 
        Activity? activity, 
        ILogger logger, 
        string operationName)
    {
        if (result.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        TraceError(activity, logger, operationName, result.Error);
        return result;
    }

    /// <summary>
    /// Async version of Trace for Task-wrapped Result.
    /// </summary>
    public static async Task<Result> TraceAsync(
        this Task<Result> task, 
        Activity? activity, 
        ILogger logger, 
        string operationName)
    {
        var result = await task;
        return result.Trace(activity, logger, operationName);
    }

    /// <summary>
    /// Async version of Trace for Task-wrapped Result&lt;T&gt;.
    /// </summary>
    public static async Task<Result<T>> TraceAsync<T>(
        this Task<Result<T>> task, 
        Activity? activity, 
        ILogger logger, 
        string operationName)
    {
        var result = await task;
        return result.Trace(activity, logger, operationName);
    }

    /// <summary>
    /// Creates a new Activity for a Result-returning operation with automatic tracing.
    /// </summary>
    /// <param name="activitySource">The activity source</param>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="operation">The operation to execute</param>
    /// <param name="logger">Logger for failures</param>
    /// <param name="tags">Optional tags to add to the activity</param>
    public static Result<T> TraceOperation<T>(
        ActivitySource activitySource,
        string operationName,
        Func<Result<T>> operation,
        ILogger logger,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = activitySource.StartActivity(operationName);
        
        if (activity != null && tags != null)
        {
            foreach (var tag in tags)
                activity.SetTag(tag.Key, tag.Value);
        }

        var result = operation();
        return result.Trace(activity, logger, operationName);
    }

    /// <summary>
    /// Async version of TraceOperation.
    /// </summary>
    public static async Task<Result<T>> TraceOperationAsync<T>(
        ActivitySource activitySource,
        string operationName,
        Func<Task<Result<T>>> operation,
        ILogger logger,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = activitySource.StartActivity(operationName);
        
        if (activity != null && tags != null)
        {
            foreach (var tag in tags)
                activity.SetTag(tag.Key, tag.Value);
        }

        var result = await operation();
        return result.Trace(activity, logger, operationName);
    }

    /// <summary>
    /// Logs only the error without affecting telemetry.
    /// Useful when you want logging without activity tracing.
    /// </summary>
    public static Result<T> LogOnFailure<T>(
        this Result<T> result,
        ILogger logger,
        string operationName)
    {
        if (!result.IsSuccess)
        {
            var level = GetLogLevel(result.Error);
            logger.Log(level, 
                "Operation {Operation} failed. Code={ErrorCode}, Target={Target}, Message={Message}",
                operationName, result.Error.Code, result.Error.Target, result.Error.Message ?? result.Error.Detail);
        }
        return result;
    }

    /// <summary>
    /// Logs only the error without affecting telemetry (non-generic version).
    /// </summary>
    public static Result LogOnFailure(
        this Result result,
        ILogger logger,
        string operationName)
    {
        if (!result.IsSuccess)
        {
            var level = GetLogLevel(result.Error);
            logger.Log(level, 
                "Operation {Operation} failed. Code={ErrorCode}, Target={Target}, Message={Message}",
                operationName, result.Error.Code, result.Error.Target, result.Error.Message ?? result.Error.Detail);
        }
        return result;
    }

    #region Private Helpers

    private static void TraceError(Activity? activity, ILogger logger, string operationName, Error error)
    {
        // Set Activity status and tags
        activity?.SetStatus(ActivityStatusCode.Error, error.Message ?? error.Code);
        activity?.SetTag("error.code", error.Code);
        activity?.SetTag("error.target", error.Target ?? string.Empty);
        activity?.SetTag("error.message", error.Message ?? string.Empty);

        // Add event for error
        activity?.AddEvent(new ActivityEvent(
            "error",
            tags: new ActivityTagsCollection
            {
                ["error.code"] = error.Code,
                ["error.target"] = error.Target ?? string.Empty
            }));

        // Log with appropriate level
        var level = GetLogLevel(error);
        logger.Log(level, 
            "Operation {Operation} failed. Code={ErrorCode}, Target={Target}, Message={Message}, Detail={Detail}",
            operationName, error.Code, error.Target, error.Message, error.Detail);
    }

    private static LogLevel GetLogLevel(Error error)
    {
        return error.Code switch
        {
            var c when c.StartsWith("validation.") => LogLevel.Warning,
            var c when c.StartsWith("notfound.") => LogLevel.Warning,
            var c when c.StartsWith("conflict.") => LogLevel.Warning,
            var c when c.StartsWith("auth.") => LogLevel.Warning,
            var c when c.StartsWith("transient.") => LogLevel.Warning,
            _ => LogLevel.Error
        };
    }

    #endregion
}

