namespace BBT.Workflow.Instances.Remote;

/// <summary>
/// This service acts as a client to the InstanceController endpoints for remote workflow instances.
/// </summary>
public interface IRemoteInstanceCommandAppService
{
    Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default);

    Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
} 