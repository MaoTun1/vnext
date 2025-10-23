using BBT.Workflow.Instances;

namespace BBT.Workflow.SubFlow;

public interface ISubflowForwardingService
{
    Task<(bool forwarded, InstanceStatus? status)> TryForwardTransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken ct);
}