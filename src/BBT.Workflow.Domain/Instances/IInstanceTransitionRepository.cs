using BBT.Aether.Domain.Repositories;

namespace BBT.Workflow.Instances;

public interface IInstanceTransitionRepository : IRepository<InstanceTransition, Guid>
{
    Task UpdateCompletedAsync(InstanceTransition transition, CancellationToken cancellationToken);
}