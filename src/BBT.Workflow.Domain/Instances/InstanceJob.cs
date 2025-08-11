using System.Text.Json;
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
        string jobId,
        string domain,
        string flowName,
        Guid instanceId,
        string expressionValue,
        JsonElement payload) : base(id)
    {
        JobName = Check.NotNull(jobName, nameof(JobName), InstanceJobConstants.MaxJobNameLength);
        JobId = Check.NotNull(jobId, nameof(JobId), InstanceJobConstants.MaxJobIdLength);
        Domain = Check.NotNull(domain, nameof(Domain), WorkflowConstants.MaxDomainLength);
        FlowName = Check.NotNull(flowName, nameof(FlowName), WorkflowConstants.MaxFlowLength);
        InstanceId = instanceId;
        ExpressionValue = Check.NotNull(expressionValue, nameof(ExpressionValue), InstanceJobConstants.MaxExpressionValueLength);
        Payload = new JsonData(payload);
        IsTriggered = false;
        CreatedAt = DateTime.UtcNow;
    }

    public string JobName { get; private set; }
    public string JobId { get; private set; }
    public string FlowName { get; private set; }
    public string Domain { get; private set; }
    public Guid InstanceId { get; private set; }
    public string ExpressionValue { get; private set; }
    public bool IsTriggered { get; private set; }
    public JsonData Payload { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public void Triggered()
    {
        IsTriggered = true;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateTriggerAt(string expressionValue)
    {
        ExpressionValue = expressionValue;
        ModifiedAt = DateTime.UtcNow;   
    }

    public static InstanceJob Create(
        Guid id,
        string jobName,
        string jobId,
        string domain,
        string flowName,
        Guid instanceId,
        string expressionValue,
        JsonElement payload)
    {
        return new InstanceJob(id, jobName, jobId, domain, flowName, instanceId, expressionValue, payload);
    }
}