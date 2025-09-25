using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Definitions;
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
    public Dictionary<string, string?> Headers { get; set; } = new();
    public Dictionary<string, string?> RouteValues { get; set; } = new();
}

public sealed class CreateInstanceInput
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(InstanceConstants.MaxKeyLength)]
    public string Key { get; set; }

    public string[]? Tags { get; set; }
    public JsonElement? Attributes { get; set; }
    public string? Callback { get; set; }
    public ObjectDictionary MetaData { get; set; } = new();
}

public sealed class StartInstanceOutput
{
    public Guid Id { get; set; }

    /// <summary>
    /// Instance status (Active, Busy, Completed, etc.)
    /// </summary>
    public InstanceStatus? Status { get; set; }
}

/// <summary>
/// Information about active SubFlow/SubProcess correlations
/// </summary>
public sealed class InstanceCorrelationInfo
{
    public Guid CorrelationId { get; set; }
    public string ParentState { get; set; } = string.Empty;
    public Guid SubFlowInstanceId { get; set; }
    public SubFlowType SubFlowType { get; set; }
    public string SubFlowDomain { get; set; } = string.Empty;
    public string SubFlowName { get; set; } = string.Empty;
    public string? SubFlowVersion { get; set; }
    public bool IsCompleted { get; set; }
}