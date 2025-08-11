using BBT.Aether.Domain.Repositories;

namespace BBT.Workflow.Instances;

public interface IInstanceTransitionRepository : IRepository<InstanceTransition, Guid>
{
    
}