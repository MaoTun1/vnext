using BBT.Aether.Application;
using BBT.Workflow.Domain;

namespace BBT.Workflow.Instances;

public interface IInstanceCommandAppService : IApplicationService
{
    Task<Result<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default);

    Task<Result<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
} 