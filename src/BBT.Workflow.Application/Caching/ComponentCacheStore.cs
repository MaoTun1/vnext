using BBT.Aether.Domain.Entities;
using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Runtime;

namespace BBT.Workflow.Caching;

/// <summary>
/// Implementation of <see cref="IComponentCacheStore"/> that provides caching operations for workflow components.
/// Uses domain-specific cache context to store and retrieve workflows, tasks, schemas, functions, views, and extensions.
/// </summary>
/// <param name="cacheContext">The domain cache context used for caching operations.</param>
public sealed class ComponentCacheStore(
    IDomainCacheContext cacheContext) : IComponentCacheStore
{
    /// <inheritdoc />
    public async Task<Result<Definitions.Workflow>> GetFlowAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<Definitions.Workflow>(
            domain,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Result<WorkflowTask>> GetTaskAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<WorkflowTask>(
            domain,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Result<SchemaDefinition>> GetSchemaAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<SchemaDefinition>(
            domain,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Result<Function>> GetFunctionAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<Function>(
            domain,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Result<View>> GetViewAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<View>(
            domain,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Result<Extension>> GetExtensionAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<Extension>(
            domain,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<Extension>>> GetAllExtensionsAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        var extensionCacheSet = GetCacheSet<Extension>();
        var extensionsResult = await extensionCacheSet.GetAllByDomainAsync(domain, cancellationToken);
        
        if (!extensionsResult.IsSuccess)
        {
            return Result<IEnumerable<Extension>>.Fail(extensionsResult.Error);
        }
        
        return Result<IEnumerable<Extension>>.Ok(extensionsResult.Value!);
    }

    /// <inheritdoc />
    public async Task<Result> SetAsync<T>(T entity, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var cacheSet = GetCacheSet<T>();
        return await cacheSet.SetAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Generic method to retrieve entities from cache with version support.
    /// </summary>
    /// <typeparam name="T">The type of entity to retrieve.</typeparam>
    /// <param name="domain">The domain identifier.</param>
    /// <param name="key">The entity key/name.</param>
    /// <param name="version">The entity version. If null or empty, retrieves the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T}"/> containing the cached entity or an error.</returns>
    private async Task<Result<T>> GetAsync<T>(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var cacheSet = GetCacheSet<T>();

        var result = version.IsNullOrEmpty()
            ? await cacheSet.GetLatestByNameAsync(domain, key, cancellationToken)
            : await cacheSet.GetAsync($"{typeof(T).Name}:{domain}:{key}:{version}", cancellationToken);

        return result;
    }

    /// <summary>
    /// Gets the appropriate cache set for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The cache set for the specified type.</returns>
    /// <remarks>
    /// Throws <see cref="NotSupportedException"/> when the entity type is not supported.
    /// This is an infrastructure configuration error and should throw per Railway Pattern guidelines.
    /// </remarks>
    private ICacheSet<T> GetCacheSet<T>() where T : class, IDomainEntity, IReferenceSetter
    {
        if (typeof(T) == typeof(Definitions.Workflow)) return (ICacheSet<T>)cacheContext.Workflows;
        if (typeof(T) == typeof(WorkflowTask)) return (ICacheSet<T>)cacheContext.Tasks;
        if (typeof(T) == typeof(SchemaDefinition)) return (ICacheSet<T>)cacheContext.Schemas;
        if (typeof(T) == typeof(Function)) return (ICacheSet<T>)cacheContext.Functions;
        if (typeof(T) == typeof(View)) return (ICacheSet<T>)cacheContext.Views;
        if (typeof(T) == typeof(Extension)) return (ICacheSet<T>)cacheContext.Extensions;

        throw new NotSupportedException($"Type {typeof(T).Name} is not supported in cache context.");
    }
}