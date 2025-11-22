using System.Text.Json;
using BBT.Aether.Events;

namespace BBT.Workflow.Instances.Events;

/// <summary>
/// Event published when a SubFlow or SubProcess instance completes.
/// Contains all necessary information about the completed SubItem instance and its data.
/// </summary>
[EventName("instance.sub.completed")]
public class InstanceSubCompletedEvent : IDistributedEvent
{
    /// <summary>
    /// The ID of the Parent instance
    /// </summary>
    [EventSubject]
    public required Guid InstanceId { get; init; }
    
    /// <summary>
    /// The domain of the parent
    /// </summary>
    public required string Domain { get; init; }
    
    /// <summary>
    /// The workflow name of the parent
    /// </summary>
    public required string Flow { get; init; }
    
    /// <summary>
    /// The version of the parent
    /// </summary>
    public required string? Version { get; init; }

    /// <summary>
    /// The ID of the completed SubItem instance
    /// </summary>
    public required Guid SubInstanceId { get; init; }
    
    /// <summary>
    /// The final state where the SubItem completed
    /// </summary>
    public required string CompletedState { get; init; }
    
    /// <summary>
    /// The complete instance data of the completed SubItem
    /// </summary>
    public JsonElement? InstanceData { get; init; }
    
    /// <summary>
    /// When the SubItem was completed
    /// </summary>
    public required DateTime CompletedAt { get; init; }
    
    /// <summary>
    /// Duration of the SubItem execution
    /// </summary>
    public TimeSpan? Duration { get; init; }
}