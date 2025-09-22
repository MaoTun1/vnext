using BBT.Aether.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Instances;

public interface IInstanceRepository : IRepository<Instance, Guid>
{
    Task<Instance?> FindByKeyAsync(string key,
        CancellationToken cancellationToken = default);
    
    Task<Instance?> FindByKeyAsReadOnlyAsync(string key,
        CancellationToken cancellationToken = default);

    Task<Instance> GetActiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<InstanceAndDataModel>> GetActiveDataListAsync(CancellationToken cancellationToken = default);

    Task<InstanceAndDataModel?> FindActiveDataAsync(string key, string version,
        CancellationToken cancellationToken = default);
    
    Task<IQueryable<Instance>> GetFilteredQueryAsync(
        string[]? filters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get filtered instances using PostgreSQL native JSON operators with advanced options
    /// </summary>
    Task<IQueryable<Instance>> GetFilteredQueryWithPostgreSqlAsync(
        string[]? filters,
        bool includeDataList = true,
        bool onlyLatest = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get filtered InstanceData directly using PostgreSQL native JSON operators
    /// </summary>
    Task<IQueryable<InstanceData>> GetFilteredInstanceDataAsync(
        string[]? filters,
        bool onlyLatest = true,
        CancellationToken cancellationToken = default);
        
    Task<Definitions.PaginationResult<Instance>> GetPagedResultsAsync(
        int page, 
        int pageSize, 
        string route, 
        IQueryCollection? queryParams = null,
        IQueryable<Instance>? instance = null,
        CancellationToken cancellationToken = default);
}