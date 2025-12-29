using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Remote;

namespace BBT.Workflow.SubFlow;

/// <summary>
/// Service for forwarding transitions to active subflow instances.
/// </summary>
public sealed class SubflowForwardingService(
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService)
    : ISubflowForwardingService
{
    /// <inheritdoc />
    public async Task<(bool forwarded, InstanceStatus? status)> TryForwardTransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken ct)
    {
        using var activity = SubFlowActivityHelper.StartActivity($"SubFlow.Forward/{input.Domain}/{input.Workflow}/{transitionKey}");
        SubFlowActivityHelper.EnrichWithForward(activity, instanceId, transitionKey);

        var result = await remoteInstanceCommandAppService
            .TransitionAsync(
                instanceId,
                transitionKey,
                input,
                ct
            );

        if (!result.IsSuccess)
        {
            activity?.SetTag("vnext.subflow.forward.result", "failed");
            SubFlowActivityHelper.SetError(activity, result.Error.Message);
            return (false, null);
        }

        var status = result.Value!.Status;
        activity?.SetTag("vnext.subflow.forward.result", "success");
        activity?.SetTag("vnext.subflow.forward.status", status.ToString());
        SubFlowActivityHelper.SetSuccess(activity);
        return (true, status);
    }
}