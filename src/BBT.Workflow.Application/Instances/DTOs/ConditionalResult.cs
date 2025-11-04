using BBT.Workflow.Domain;

namespace BBT.Workflow.Instances;

/// <summary>
/// Represents a result that may indicate a conditional HTTP response (e.g., 304 Not Modified).
/// Wraps Result&lt;T&gt; with additional metadata for conditional requests.
/// </summary>
/// <typeparam name="T">The type of the value returned on success</typeparam>
public readonly record struct ConditionalResult<T>
{
    /// <summary>
    /// The underlying result
    /// </summary>
    public Result<T> Result { get; }
    
    /// <summary>
    /// Indicates if the resource was not modified (for ETag handling - HTTP 304)
    /// </summary>
    public bool IsNotModified { get; }

    private ConditionalResult(Result<T> result, bool isNotModified)
    {
        Result = result;
        IsNotModified = isNotModified;
    }

    /// <summary>
    /// Creates a 304 Not Modified conditional result
    /// </summary>
    public static ConditionalResult<T> NotModified()
        => new(Result<T>.Ok(default(T)!), true);

    /// <summary>
    /// Creates a successful conditional result with data
    /// </summary>
    public static ConditionalResult<T> Success(T value)
        => new(Result<T>.Ok(value), false);

    /// <summary>
    /// Creates a failed conditional result
    /// </summary>
    public static ConditionalResult<T> Fail(Error error)
        => new(Result<T>.Fail(error), false);

    /// <summary>
    /// Implicit conversion from Result&lt;T&gt; for backward compatibility
    /// </summary>
    public static implicit operator ConditionalResult<T>(Result<T> result)
        => new(result, false);

    /// <summary>
    /// Implicit conversion to Result&lt;T&gt;
    /// </summary>
    public static implicit operator Result<T>(ConditionalResult<T> conditionalResult)
        => conditionalResult.Result;
}

