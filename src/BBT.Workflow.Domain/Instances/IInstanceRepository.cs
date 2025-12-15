using BBT.Aether;
using BBT.Aether.Domain.Repositories;
using BBT.Aether.Results;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Instances;

public interface IInstanceRepository : IRepository<Instance, Guid>
{
    Task<Instance?> FindByIdentifierAsync(string? identifier,
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

    /// <summary>
    /// Checks if an active instance exists with the specified key, excluding the given instance ID.
    /// </summary>
    /// <param name="key">The key to check for duplicates.</param>
    /// <param name="excludeInstanceId">The instance ID to exclude from the check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an active instance with the same key exists, false otherwise.</returns>
    Task<bool> AnyActiveByKeyAsync(string key, Guid excludeInstanceId, CancellationToken cancellationToken = default);
}