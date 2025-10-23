using BBT.Aether.Domain.Entities;
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
    public async Task<Definitions.Workflow> GetFlowAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<Definitions.Workflow>(
            domain,
            RuntimeSysSchemaInfo.Flows,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<WorkflowTask> GetTaskAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<WorkflowTask>(
            domain,
            RuntimeSysSchemaInfo.Tasks,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<SchemaDefinition> GetSchemaAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<SchemaDefinition>(
            domain,
            RuntimeSysSchemaInfo.Schemas,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Function> GetFunctionAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<Function>(
            domain,
            RuntimeSysSchemaInfo.Functions,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<View> GetViewAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<View>(
            domain,
            RuntimeSysSchemaInfo.Views,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Extension> GetExtensionAsync(string domain, string key, string? version,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<Extension>(
            domain,
            RuntimeSysSchemaInfo.Extensions,
            key,
            version,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Extension>> GetAllExtensionsAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        var extensionCacheSet = GetCacheSet<Extension>();
        var extensions = await extensionCacheSet.GetAllByDomainAsync(domain, cancellationToken);
        return extensions;
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(T entity, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var cacheSet = GetCacheSet<T>();
        await cacheSet.SetAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Generic method to retrieve entities from cache with version support.
    /// </summary>
    /// <typeparam name="T">The type of entity to retrieve.</typeparam>
    /// <param name="domain">The domain identifier.</param>
    /// <param name="flow">The flow/schema type identifier from <see cref="RuntimeSysSchemaInfo"/>.</param>
    /// <param name="key">The entity key/name.</param>
    /// <param name="version">The entity version. If null or empty, retrieves the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The cached entity of type T.</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the entity is not found in cache.</exception>
    private async Task<T> GetAsync<T>(
        string domain,
        string flow,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var cacheSet = GetCacheSet<T>();

        var entity = version.IsNullOrEmpty()
            ? await cacheSet.GetLatestByNameAsync(domain,flow, key, cancellationToken)
            : await cacheSet.GetAsync($"{typeof(T).Name}:{domain}:{flow}:{key}:{version}", cancellationToken);

        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(T), new { domain, key, version });
        }

        return entity;
    }

    /// <summary>
    /// Gets the appropriate cache set for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The cache set for the specified type.</returns>
    /// <exception cref="NotSupportedException">Thrown when the entity type is not supported in cache context.</exception>
    private ICacheSet<T> GetCacheSet<T>() where T : class, IDomainEntity, IReferenceSetter
    {
        if (typeof(T) == typeof(Definitions.Workflow)) return (ICacheSet<T>)(object)cacheContext.Workflows;
        if (typeof(T) == typeof(WorkflowTask)) return (ICacheSet<T>)(object)cacheContext.Tasks;
        if (typeof(T) == typeof(SchemaDefinition)) return (ICacheSet<T>)(object)cacheContext.Schemas;
        if (typeof(T) == typeof(Function)) return (ICacheSet<T>)(object)cacheContext.Functions;
        if (typeof(T) == typeof(View)) return (ICacheSet<T>)(object)cacheContext.Views;
        if (typeof(T) == typeof(Extension)) return (ICacheSet<T>)(object)cacheContext.Extensions;

        throw new NotSupportedException($"Type {typeof(T).Name} is not supported in cache context.");
    }
}