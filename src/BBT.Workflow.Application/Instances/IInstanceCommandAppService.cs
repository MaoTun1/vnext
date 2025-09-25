using BBT.Aether.Application;

namespace BBT.Workflow.Instances;

public interface IInstanceCommandAppService : IApplicationService
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