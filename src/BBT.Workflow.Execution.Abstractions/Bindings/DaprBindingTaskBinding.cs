namespace BBT.Workflow.Execution.Bindings;

/// <summary>
/// Binding configuration for Dapr output binding task.
/// </summary>
public sealed class DaprBindingTaskBinding
{
    /// <summary>
    /// The name of the Dapr binding to invoke.
    /// </summary>
    public required string BindingName { get; init; }
    
    /// <summary>
    /// The operation to perform on the binding (e.g., "create", "get", "delete").
    /// </summary>
    public string Operation { get; init; } = "create";
    
    /// <summary>
    /// Request body/data as JSON string.
    /// </summary>
    public string? Body { get; init; }
    
    /// <summary>
    /// Additional metadata for the binding invocation.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

