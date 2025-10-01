using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Output for retrieving active correlations
/// </summary>
public sealed class GetActiveCorrelationsOutput
{
    /// <summary>
    /// Active correlations with href links
    /// </summary>
    public List<ActiveCorrelationHref> ActiveCorrelations { get; set; } = [];

    /// <summary>
    /// Instance status
    /// </summary>
    public InstanceStatus? Status { get; set; }

    /// <summary>
    /// Current state of the instance
    /// </summary>
    public string? CurrentState { get; set; }
}

