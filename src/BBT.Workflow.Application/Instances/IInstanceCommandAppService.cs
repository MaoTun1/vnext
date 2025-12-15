using BBT.Aether.Application;
using BBT.Aether.Results;
using BBT.Workflow.Domain;

namespace BBT.Workflow.Instances;

public interface IInstanceCommandAppService : IApplicationService
{
    Task<Result<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default);

    Task<Result<TransitionOutput>> TransitionAsync(
        string instance,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
} 