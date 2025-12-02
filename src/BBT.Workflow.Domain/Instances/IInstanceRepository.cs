using BBT.Aether.Domain.Repositories;
using BBT.Aether.Results;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Instances;

public interface IInstanceRepository : IRepository<Instance, Guid>
{
    Task<Instance?> FindByIdentifierAsync(string identifier,
        CancellationToken cancellationToken = default);
    
    Task<Instance?> FindByIdentifierAsReadOnlyAsync(string identifier,
        CancellationToken cancellationToken = default);

    Task<Result<Instance>> GetActiveAsync(string identifier, CancellationToken cancellationToken = default);
    
    Task<List<InstanceAndDataModel>> GetActiveDataListAsync(CancellationToken cancellationToken = default);

    Task<InstanceAndDataModel?> FindActiveDataAsync(string key, string version,
        CancellationToken cancellationToken = default);
        
    Task<HateoasPagedList<Instance>> GetPagedResultsAsync(
        int page, 
        int pageSize, 
        string[]? filters,
        CancellationToken cancellationToken = default);

    Task<Result<Instance>> GetResultAsync(string identifier, bool includeDetails = true,
        CancellationToken cancellationToken = default);
}