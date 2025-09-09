using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Instances;

public sealed class StartInstanceInput(
    string domain,
    string workflow,
    string? version = null,
    bool sync = false) : IHasDomain
{
    public string Domain { get; set; } = domain;
    public string Workflow { get; set; } = workflow;
    public string? Version { get; set; } = version;
    public bool Sync { get; set; } = sync;
    public CreateInstanceInput Instance { get; set; }
    public Dictionary<string, string>? Headers { get; set; } = new();
    public Dictionary<string, string?>? RouteValues { get; set; } = new();
}

public sealed class CreateInstanceInput
{
    [Required]
    [StringLength(InstanceConstants.MaxKeyLength)]
    public string Key { get; set; }
    public string[]? Tags { get; set; }
    public JsonElement? Attributes { get; set; }
}

public sealed class StartInstanceOutput
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Instance status (Active, Busy, Completed, etc.)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Current state of the instance
    /// </summary>
    public string? CurrentState { get; set; }

    /// <summary>
    /// Active SubFlow/SubProcess correlations for this instance
    /// </summary>
    public List<InstanceCorrelationInfo> ActiveCorrelations { get; set; } = [];
}

/// <summary>
/// Information about active SubFlow/SubProcess correlations
/// </summary>
 public sealed class InstanceCorrelationInfo
{
    public Guid CorrelationId { get; set; }
    public string ParentState { get; set; } = string.Empty;
    public Guid SubFlowInstanceId { get; set; }
    public string SubFlowType { get; set; } = string.Empty;
    public string SubFlowDomain { get; set; } = string.Empty;
    public string SubFlowName { get; set; } = string.Empty;
    public string? SubFlowVersion { get; set; }
    public bool IsCompleted { get; set; }
}