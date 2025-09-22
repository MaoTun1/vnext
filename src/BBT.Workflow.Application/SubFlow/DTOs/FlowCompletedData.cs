using System.Text.Json;

namespace BBT.Workflow.SubFlow;

/// <summary>
/// Data payload for flow completion event.
/// Contains all necessary information about the completed flow instance and its data.
/// </summary>
public record FlowCompletedData
{
    /// <summary>
    /// The ID of the completed flow instance
    /// </summary>
    public required Guid InstanceId { get; init; }
    
    /// <summary>
    /// The domain of the completed flow
    /// </summary>
    public required string Domain { get; init; }
    
    /// <summary>
    /// The workflow name of the completed flow
    /// </summary>
    public required string Workflow { get; init; }
    
    /// <summary>
    /// The version of the completed flow
    /// </summary>
    public required string Version { get; init; }
    
    /// <summary>
    /// The final state where the flow completed
    /// </summary>
    public required string CompletedState { get; init; }
    
    /// <summary>
    /// The complete instance data of the completed flow
    /// </summary>
    public JsonElement? InstanceData { get; init; }
    
    /// <summary>
    /// MetaData associated with the completed flow instance
    /// </summary>
    public ObjectDictionary MetaData { get; init; }
    
    /// <summary>
    /// When the flow was completed
    /// </summary>
    public required DateTime CompletedAt { get; init; }
    
    /// <summary>
    /// Duration of the flow execution
    /// </summary>
    public TimeSpan? Duration { get; init; }
}
