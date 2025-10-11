using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Remote;

namespace BBT.Workflow.SubFlow;

public sealed class SubflowForwardingService(
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService)
    : ISubflowForwardingService
{
    public async Task<(bool forwarded, InstanceStatus? status)> TryForwardTransitionAsync(Guid instanceId,
        string transitionKey, TransitionInput input,
        CancellationToken ct)
    {
        var response = await remoteInstanceCommandAppService
            .TransitionAsync(
                instanceId,
                transitionKey,
                input,
                ct
            );

        return (true, response.Data.Status);
    }
}