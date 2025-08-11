using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Input for retrieving instance available Transitions
/// </summary>
public class GetAvailableTransitionInput : IHasDomain
{
    [Required]
    [StringLength(WorkflowConstants.MaxDomainLength)]
    public string Domain { get; set; } = string.Empty;

    [Required]
    [StringLength(WorkflowConstants.MaxFlowLength)]
    public string Workflow { get; set; } = string.Empty;

    [StringLength(WorkflowConstants.MaxVersionLength)]
    public string? Version { get; set; } = string.Empty;

    [Required] public string Instance { get; set; } = string.Empty;
}

public sealed class GetAvailableTransitionOutput
{
    public GetAvailableTransitionOutput()
    {
    }

    public GetAvailableTransitionOutput(List<string> items)
    {
        Items = items;
    }

    /// <summary>
    /// Available transition keys for the instance
    /// </summary>
    public List<string> Items { get; set; } = [];

    /// <summary>
    /// Instance status (Active, Busy, Completed, etc.)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Current state of the instance
    /// </summary>
    public string? CurrentState { get; set; }
}