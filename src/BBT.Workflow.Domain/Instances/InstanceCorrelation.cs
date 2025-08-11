using BBT.Aether;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Instance Correlation
/// </summary>
public sealed class InstanceCorrelation : Entity<Guid>
{
    private InstanceCorrelation()
    {
    }

    public InstanceCorrelation(
        Guid id,
        Guid instanceId,
        string parentState,
        Guid subFlowInstanceId,
        string subFlowType,
        string subFlowDomain,
        string subFlowName,
        string? subFlowVersion
    ) : base(id)
    {
        ParentInstanceId = instanceId;
        ParentState = Check.NotNullOrEmpty(parentState, nameof(parentState), StateConstants.MaxKeyLength);
        SubFlowInstanceId = subFlowInstanceId;
        SubFlowType = SubFlowType.FromCode(subFlowType);
        IsCompleted = false;
        SubFlowDomain = Check.NotNullOrEmpty(subFlowDomain, nameof(subFlowDomain), WorkflowConstants.MaxDomainLength);
        SubFlowName = Check.NotNullOrEmpty(subFlowName, nameof(subFlowName), WorkflowConstants.MaxKeyLength);
        SubFlowVersion = Check.Length(subFlowVersion, nameof(subFlowVersion), WorkflowConstants.MaxVersionLength);
    }

    /// <summary>
    /// <see cref="Instance"/> Parent Instance ID
    /// </summary>
    public Guid ParentInstanceId { get; private set; }

    /// <summary>
    /// <see cref="State"/> Parent State ID
    /// </summary>
    public string ParentState { get; private set; }

    /// <summary>
    /// <see cref="Instance"/> SubFlow Instance ID
    /// </summary>
    public Guid SubFlowInstanceId { get; private set; }

    /// <summary>
    /// Sub Flow Domain
    /// </summary>
    public string SubFlowDomain { get; private set; }

    /// <summary>
    /// Sub Flow Name
    /// </summary>
    public string SubFlowName { get; private set; }

    /// <summary>
    /// Sub Flow Version
    /// </summary>
    public string? SubFlowVersion { get; private set; }

    /// <summary>
    /// SubFlow Type: "S" (SubFlow - blocking) or "P" (SubProcess - non-blocking)
    /// This field enables performant querying without joining to Instance table.
    /// </summary>
    public SubFlowType SubFlowType { get; private set; }

    /// <summary>
    /// Is completed
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Completed at
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    public void Complete()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
    }
}