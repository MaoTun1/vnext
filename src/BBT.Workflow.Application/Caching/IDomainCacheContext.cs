using BBT.Workflow.Definitions;

namespace BBT.Workflow.Caching;

/// <summary>
/// Interface for domain cache context
/// </summary>
public interface IDomainCacheContext
{
    ICacheSet<Definitions.Workflow> Workflows { get; }
    ICacheSet<WorkflowTask> Tasks { get; }
    ICacheSet<SchemaDefinition> Schemas { get; }
    ICacheSet<Function> Functions { get; }
    ICacheSet<View> Views { get; }
    ICacheSet<Extension> Extensions { get; }

    Task InitializeAsync(Dictionary<Type, object> initialData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cache set for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>The cache set for the entity type</returns>
    ICacheSet<T> Set<T>() where T : class, IDomainEntity, IReferenceSetter;

    /// <summary>
    /// Performs cleanup on all cache sets, removing expired and least-used items.
    /// </summary>
    /// <param name="ttl">Time-to-live for cache items</param>
    /// <param name="maxItemsPerSet">Maximum items per cache set</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>Total number of items removed across all cache sets</returns>
    int CleanupAll(
        TimeSpan? ttl = null,
        int? maxItemsPerSet = null,
        CancellationToken cancellationToken = default);
}

