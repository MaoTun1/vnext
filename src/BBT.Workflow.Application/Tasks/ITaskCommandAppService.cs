using BBT.Aether.Application;
using BBT.Aether.Results;
using BBT.Workflow.Domain;

namespace BBT.Workflow.Tasks;

public interface ITaskCommandAppService : IApplicationService
{
    Task<Result<TaskContextUpdateOutput>> ExecuteTaskAsync(
        TaskExecutionRequestInput input,
        CancellationToken cancellationToken = default);
}