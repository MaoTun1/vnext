using BBT.Aether.Events;

namespace BBT.Workflow.Instances.Events;

/// <summary>
/// Event published when a workflow instance is canceled.
/// Contains all necessary information about the canceled instance.
/// </summary>
[EventName("instance.canceled")]
public class InstanceCanceledEvent : IDistributedEvent
{
    /// <summary>
    /// The ID of the canceled instance
    /// </summary>
    [EventSubject]
    public required Guid InstanceId { get; init; }

    /// <summary>
    /// The workflow name of the canceled instance
    /// </summary>
    public required string Flow { get; init; }
    
    /// <summary>
    /// The state where the instance was canceled
    /// </summary>
    public required string CanceledState { get; init; }
    
    /// <summary>
    /// When the instance was canceled
    /// </summary>
    public required DateTime CanceledAt { get; init; }
    
    /// <summary>
    /// Duration of the instance execution before cancellation
    /// </summary>
    public TimeSpan? Duration { get; init; }
}

