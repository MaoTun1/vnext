namespace BBT.Workflow.Execution.Bindings;

/// <summary>
/// Binding configuration for GetInstanceData task execution.
/// Used to retrieve instance data from a remote domain.
/// </summary>
public sealed class GetInstanceDataBinding
{
    /// <summary>
    /// Target domain where the workflow instance resides.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Target workflow key.
    /// </summary>
    public required string Workflow { get; init; }

    /// <summary>
    /// Target instance ID (can be Guid string or key).
    /// </summary>
    public required string Instance { get; init; }

    /// <summary>
    /// Optional extensions to include in the response.
    /// </summary>
    public string[]? Extensions { get; init; }

    /// <summary>
    /// Optional ETag for conditional request.
    /// </summary>
    public string? ETag { get; init; }
}

