namespace BBT.Workflow.Instances;

/// <summary>
/// Output for retrieving transition items
/// </summary>
public sealed class GetTransitionItemsOutput
{
    /// <summary>
    /// Available transition items with href links
    /// </summary>
    public List<TransitionItem> Items { get; set; } = [];

    /// <summary>
    /// Instance status
    /// </summary>
    public InstanceStatus? Status { get; set; }

    /// <summary>
    /// Current state of the instance
    /// </summary>
    public string? CurrentState { get; set; }
}

