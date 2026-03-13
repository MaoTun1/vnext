using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Aether;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution;

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

    /// <summary>
    /// When true, returns 409 Conflict if an active instance with same key exists.
    /// Used for service-to-service calls to prevent false positive correlations.
    /// Default false preserves idempotent behavior for client calls.
    /// </summary>
    public bool StrictIdempotency { get; set; } = false;

    /// <summary>
    /// Creates a WorkflowExecutionContext from this StartInstanceInput for starting a new workflow instance.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier</param>
    /// <param name="startTransitionKey">The start transition key to execute</param>
    /// <returns>A new WorkflowExecutionContext instance</returns>
    public WorkflowExecutionContext ToExecutionContext(Guid instanceId, string startTransitionKey)
    {
        return new WorkflowExecutionContext
        {
            Domain = Domain,
            InstanceId = instanceId.ToString(),
            WorkflowKey = Workflow,
            WorkflowVersion = Version,
            TransitionKey = startTransitionKey,
            TriggerType = TriggerType.Manual, // Start transitions are always manual,
            Mode = Sync ? ExecMode.Sync : ExecMode.Async,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAt = DateTimeOffset.UtcNow,
            Headers = Headers,
            RouteValues = RouteValues,
            Data = new TransitionDataInfo(Instance.Attributes)
            {
                Tags = Instance.Tags
            },
            IsReentry = false // Start transitions are never re-entry
        };
    }
}

public sealed class CreateInstanceInput: IHasExtraProperties
{
    public Guid? Id { get; set; }
    
    [StringLength(InstanceConstants.MaxKeyLength)]
    public string? Key { get; set; }

    public string[]? Tags { get; set; }
    public JsonElement? Attributes { get; set; }
    public string? Callback { get; set; }
    public ExtraPropertyDictionary ExtraProperties { get; set; } = new();
}

public sealed class StartInstanceOutput
{
    public Guid Id { get; set; }
    public string? Key { get; set; }

    /// <summary>
    /// Instance status (Active, Busy, Completed, etc.)
    /// </summary>
    public InstanceStatus? Status { get; set; }

    /// <summary>
    /// Instance attributes filtered by master-schema role grants. Populated only when sync=true.
    /// </summary>
    public JsonElement? Attributes { get; set; }

    /// <summary>
    /// Representation ETag (SHA256 of canonical response JSON), returned with quotes per RFC 7232. Populated only when sync=true.
    /// </summary>
    public string? ETag
    {
        get => _etag is null ? null : $"\"{_etag.Replace("\"", "")}\"";
        set => _etag = value;
    }
    private string? _etag;

    /// <summary>
    /// Entity (DB row) version for concurrency, returned with quotes per RFC 7232. Populated only when sync=true.
    /// </summary>
    public string? EntityEtag
    {
        get => _entityEtag is null ? null : $"\"{_entityEtag.Replace("\"", "")}\"";
        set => _entityEtag = value;
    }
    private string? _entityEtag;

    /// <summary>
    /// Computed extension fields. Populated only when sync=true.
    /// </summary>
    public Dictionary<string, object>? Extensions { get; set; }
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