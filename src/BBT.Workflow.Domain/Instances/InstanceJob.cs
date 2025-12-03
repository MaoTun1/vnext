using BBT.Aether;
using BBT.Aether.Auditing;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

public class InstanceJob : Entity<Guid>, IHasCreatedAt, IHasModifyTime
{
    private InstanceJob()
    {
    }

    internal InstanceJob(
        Guid id,
        string jobName,
        Guid jobId,
        string domain,
        string flowName,
        Guid instanceId) : base(id)
    {
        JobName = Check.NotNullOrWhiteSpace(jobName, nameof(JobName), InstanceJobConstants.MaxJobNameLength);
        JobId = jobId;
        Domain = Check.NotNullOrWhiteSpace(domain, nameof(Domain), WorkflowConstants.MaxDomainLength);
        FlowName = Check.NotNullOrWhiteSpace(flowName, nameof(FlowName), WorkflowConstants.MaxFlowLength);
        InstanceId = instanceId;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public string JobName { get; private set; }
    public Guid JobId { get; private set; }
    public string FlowName { get; private set; }
    public string Domain { get; private set; }
    public Guid InstanceId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public void MarkAsProcessed()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }

    public static InstanceJob Create(
        Guid id,
        string jobName,
        Guid jobId,
        string domain,
        string flowName,
        Guid instanceId)
    {
        return new InstanceJob(id, jobName, jobId, domain, flowName, instanceId);
    }
}