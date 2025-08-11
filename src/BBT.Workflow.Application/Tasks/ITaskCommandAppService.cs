using BBT.Aether.Application;

namespace BBT.Workflow.Tasks;

public interface ITaskCommandAppService : IApplicationService
{
    Task<TaskContextUpdateOutput> ExecuteTaskAsync(
        TaskExecutionRequestInput input,
        CancellationToken cancellationToken = default);
}