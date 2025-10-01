using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Input for retrieving active correlations
/// </summary>
public sealed class GetActiveCorrelationsInput : IHasDomain
{
    [Required]
    [StringLength(WorkflowConstants.MaxDomainLength)]
    public string Domain { get; set; } = string.Empty;

    [Required]
    [StringLength(WorkflowConstants.MaxFlowLength)]
    public string Workflow { get; set; } = string.Empty;

    [StringLength(WorkflowConstants.MaxVersionLength)]
    public string? Version { get; set; } = string.Empty;

    [Required] 
    public string Instance { get; set; } = string.Empty;
}

