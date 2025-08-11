using BBT.Aether.Domain.Repositories;

namespace BBT.Workflow.Instances;

public interface IInstanceJobRepository : IRepository<InstanceJob, Guid>
{
    Task<bool> ExistsAsync(string jobId, CancellationToken cancellationToken = default);
    Task<InstanceJob?> FindByNameAsync(string jobId, CancellationToken cancellationToken = default);
    Task<List<InstanceJob>> GetListUntriggeredAsync(CancellationToken cancellationToken = default);
}