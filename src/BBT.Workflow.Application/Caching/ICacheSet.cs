namespace BBT.Workflow.Caching;

/// <summary>
/// Non-generic marker interface for cache sets
/// </summary>
public interface ICacheSet : IDisposable
{
    Task LoadAllAsync(object data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic interface for strongly-typed cache set operations
/// </summary>
/// <typeparam name="T">The type of entity to cache</typeparam>
public interface ICacheSet<T> : ICacheSet where T : class, IDomainEntity, IReferenceSetter
{
    Task<T?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync(T entity, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllByNameAsync(string domain, string name, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllByDomainAsync(string domain, CancellationToken cancellationToken = default);
    Task<T?> GetLatestByNameAsync(string domain, string flow, string name, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default);
}