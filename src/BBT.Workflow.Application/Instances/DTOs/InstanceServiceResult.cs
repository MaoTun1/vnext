using BBT.Workflow.Shared;

namespace BBT.Workflow.Instances;

/// <summary>
/// Specialized service result for instance operations that may return conditional responses
/// </summary>
public sealed class InstanceServiceResult<T> : ServiceResponse<T, InstanceServiceResult<T>>
{
    public InstanceServiceResult(T data) : base(data)
    {
    }

    public InstanceServiceResult(T data, bool isNotModified) : base(data)
    {
        IsNotModified = isNotModified;
    }

    /// <summary>
    /// Indicates if the resource was not modified (for ETag handling)
    /// </summary>
    public bool IsNotModified { get; set; }

    /// <summary>
    /// Creates a 304 Not Modified result
    /// </summary>
    public static InstanceServiceResult<T> NotModified()
    {
        return new InstanceServiceResult<T>(default(T)!, true);
    }

    /// <summary>
    /// Creates a successful result with data
    /// </summary>
    public static InstanceServiceResult<T> Success(T data)
    {
        return new InstanceServiceResult<T>(data, false);
    }
} 