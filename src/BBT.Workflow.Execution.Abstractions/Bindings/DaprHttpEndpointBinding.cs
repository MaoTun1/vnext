namespace BBT.Workflow.Execution.Bindings;

/// <summary>
/// Binding configuration for Dapr HTTP endpoint task.
/// </summary>
public sealed class DaprHttpEndpointBinding
{
    /// <summary>
    /// The name of the Dapr HTTP endpoint (configured in Dapr components).
    /// </summary>
    public required string EndpointName { get; init; }
    
    /// <summary>
    /// The path/route to invoke on the endpoint.
    /// </summary>
    public required string Path { get; init; }
    
    /// <summary>
    /// HTTP method for the invocation (defaults to GET).
    /// </summary>
    public string Method { get; init; } = "GET";
    
    /// <summary>
    /// Request body as JSON string.
    /// </summary>
    public string? Body { get; init; }
}

