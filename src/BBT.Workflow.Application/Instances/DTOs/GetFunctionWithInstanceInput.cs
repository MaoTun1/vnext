using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Input for retrieving function result for an instance
/// </summary>
public sealed class GetFunctionWithInstanceInput : IHasDomain
{
    [Required]
    [StringLength(WorkflowConstants.MaxDomainLength)]
    public string Domain { get; set; } = string.Empty;

    [Required] 
    [StringLength(WorkflowConstants.MaxFlowLength)]
    public string Workflow { get; set; } = string.Empty;

    [Required]
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// Function name (e.g., "state", "view", "data")
    /// </summary>
    [Required]
    public string Function { get; set; } = string.Empty;

    /// <summary>
    /// Version of the workflow
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Extensions to be appended to the data href URL
    /// </summary>
    public string[]? Extension { get; set; }
}

