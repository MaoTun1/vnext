using BBT.Aether.Domain.Repositories;

namespace BBT.Workflow.Instances;

public interface IInstanceTaskRepository : IRepository<InstanceTask, Guid>
{
    
}