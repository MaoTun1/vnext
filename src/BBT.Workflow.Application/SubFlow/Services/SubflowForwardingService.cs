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
        var result = await remoteInstanceCommandAppService
            .TransitionAsync(
                instanceId,
                transitionKey,
                input,
                ct
            );

        if (!result.IsSuccess)
        {
            return (false, null);
        }

        return (true, result.Value!.Status);
    }
}