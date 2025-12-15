using BBT.Aether.Results;

namespace BBT.Workflow.Caching;

/// <summary>
/// Centralized factory methods for cache-related domain errors.
/// Provides strongly-typed error creation to reduce string noise and improve code readability.
/// </summary>
/// <remarks>
/// Usage pattern:
/// <code>
/// // Before:
/// Error.NotFound(WorkflowErrorCodes.CacheItemNotFound, $"{typeof(T).Name} not found in runtime backend", $"{domain}/{key}@{version}")
/// 
/// // After:
/// CacheErrors.ItemNotFoundInBackend&lt;T&gt;(domain, key, version)
/// </code>
/// </remarks>
public static class CacheErrors
{
    #region Cache Item Errors

    /// <summary>
    /// Creates an error when an entity is not found in the cache or runtime backend.
    /// </summary>
    /// <typeparam name="T">The type of entity that was not found.</typeparam>
    /// <param name="domain">The domain where the entity was searched.</param>
    /// <param name="key">The key of the entity.</param>
    /// <param name="version">The version of the entity (use "latest" if searching for latest version).</param>
    public static Error ItemNotFoundInBackend<T>(string domain, string key, string? version)
        => Error.NotFound(
            WorkflowErrorCodes.CacheItemNotFound,
            $"{typeof(T).Name} not found in runtime backend",
            target: $"{domain}/{key}@{version ?? "latest"}");

    /// <summary>
    /// Creates an error when an entity is not found in the cache or runtime backend.
    /// </summary>
    /// <param name="typeName">The name of the entity type that was not found.</param>
    /// <param name="domain">The domain where the entity was searched.</param>
    /// <param name="key">The key of the entity.</param>
    /// <param name="version">The version of the entity (use "latest" if searching for latest version).</param>
    public static Error ItemNotFoundInBackend(string typeName, string domain, string key, string? version)
        => Error.NotFound(
            WorkflowErrorCodes.CacheItemNotFound,
            $"{typeName} not found in runtime backend",
            target: $"{domain}/{key}@{version ?? "latest"}");

    /// <summary>
    /// Creates an error when trying to set a null entity to cache.
    /// </summary>
    public static Error EntityCannotBeNull()
        => Error.Validation(
            WorkflowErrorCodes.CacheItemNotFound,
            "Entity cannot be null",
            target: "entity");

    #endregion

    #region Cache Key Errors

    /// <summary>
    /// Creates an error when cache key format is invalid.
    /// </summary>
    /// <param name="cacheKey">The invalid cache key.</param>
    public static Error InvalidCacheKeyFormat(string cacheKey)
        => Error.Validation(
            WorkflowErrorCodes.CacheInvalidKey,
            $"Invalid cache key format: {cacheKey}",
            target: cacheKey);

    #endregion

    #region Cache Type Errors

    /// <summary>
    /// Creates an error when cache type is not supported.
    /// </summary>
    /// <typeparam name="T">The unsupported type.</typeparam>
    public static Error TypeNotSupported<T>()
        => Error.NotSupported(
            WorkflowErrorCodes.CacheTypeNotSupported,
            $"Type {typeof(T).Name} is not supported in cache context.",
            target: typeof(T).Name);

    /// <summary>
    /// Creates an error when cache type is not supported.
    /// </summary>
    /// <param name="typeName">The name of the unsupported type.</param>
    public static Error TypeNotSupported(string typeName)
        => Error.NotSupported(
            WorkflowErrorCodes.CacheTypeNotSupported,
            $"Type {typeName} is not supported in cache context.",
            target: typeName);

    #endregion
}

