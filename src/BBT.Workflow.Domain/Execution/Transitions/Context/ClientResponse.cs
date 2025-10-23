using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution;

public class ClientResponse
{
    public Guid Id { get; set; }
    public InstanceStatus Status { get; set; }
}