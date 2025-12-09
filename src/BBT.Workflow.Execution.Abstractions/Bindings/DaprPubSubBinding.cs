namespace BBT.Workflow.Execution.Bindings;

/// <summary>
/// Binding configuration for Dapr PubSub task.
/// </summary>
public sealed class DaprPubSubBinding
{
    /// <summary>
    /// The name of the Dapr pub/sub component.
    /// </summary>
    public required string PubSubName { get; init; }
    
    /// <summary>
    /// The topic name to publish to.
    /// </summary>
    public required string TopicName { get; init; }
    
    /// <summary>
    /// Message body/data as JSON string.
    /// </summary>
    public string? Body { get; init; }
    
    /// <summary>
    /// Additional metadata for the publish operation.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

