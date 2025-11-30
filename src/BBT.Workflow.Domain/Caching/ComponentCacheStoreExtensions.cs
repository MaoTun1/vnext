using BBT.Aether.Results;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Caching;

/// <summary>
/// Extension methods for <see cref="IComponentCacheStore"/> providing reference-based cache operations.
/// </summary>
public static class ComponentCacheStoreExtensions
{
    /// <summary>
    /// Retrieves a workflow definition from the cache using a reference.
    /// </summary>
    public static Task<Result<Definitions.Workflow>> GetFlowAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetFlowAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    /// <summary>
    /// Retrieves a workflow task definition from the cache using a reference.
    /// </summary>
    public static Task<Result<WorkflowTask>> GetTaskAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetTaskAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    /// <summary>
    /// Retrieves a schema definition from the cache using a reference.
    /// </summary>
    public static Task<Result<SchemaDefinition>> GetSchemaAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetSchemaAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    /// <summary>
    /// Retrieves a function definition from the cache using a reference.
    /// </summary>
    public static Task<Result<Function>> GetFunctionAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetFunctionAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    /// <summary>
    /// Retrieves a view definition from the cache using a reference.
    /// </summary>
    public static Task<Result<View>> GetViewAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetViewAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    /// <summary>
    /// Retrieves an extension definition from the cache using a reference.
    /// </summary>
    public static Task<Result<Extension>> GetExtensionAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetExtensionAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);
}