namespace BBT.Workflow.Instances;

/// <summary>
/// Output from a transition execution containing the instance ID and current status.
/// </summary>
public sealed class TransitionOutput
{
    /// <summary>
    /// The workflow instance identifier.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Instance status (Active, Busy, Completed, etc.)
    /// </summary>
    public InstanceStatus? Status { get; set; }
}

