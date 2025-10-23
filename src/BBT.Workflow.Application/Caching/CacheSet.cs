using System.Text.RegularExpressions;
using BBT.Aether.DistributedCache;
using BBT.Workflow.Definitions;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Caching;

public class CacheSet<T>(
    Func<IDistributedCacheService> cacheResolver,
    ILogger logger,
    IServiceProvider serviceProvider) : ICacheSet<T>
    where T : class, IDomainEntity, IReferenceSetter
{
    private readonly ReaderWriterLockSlim _cacheLock = new();
    private Dictionary<string, CacheItem<T>> _localCache = new();
    private readonly Dictionary<string, SortedSet<string>> _index = new();
    private DateTime _lastCleanupTime = DateTime.UtcNow;
    private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1); // Cleanup in order

    // Cache configuration
    private const int DefaultMaxCacheSize = 1000;
    private const int DefaultCleanupThreshold = 1200; // Trigger cleanup when cache exceeds this size
    private static readonly TimeSpan DefaultMaxCacheAge = TimeSpan.FromHours(2);
    private static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10); // Add Lock timeout

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),
    };

    public async Task LoadAllAsync(object data, CancellationToken cancellationToken = default)
    {
        if (data is not IEnumerable<T> entities)
            throw new ArgumentException($"Invalid data type for {typeof(T).Name}");

        var allEntities = new Dictionary<string, CacheItem<T>>();
        var allIndex = new Dictionary<string, SortedSet<string>>();
        foreach (var entity in entities)
        {
            var cacheKey = entity.CacheKey;
            try
            {
                await cacheResolver().SetAsync(cacheKey, entity, CacheOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cache entity during load: {CacheKey}. Cache may not be available.", cacheKey);
            }
            allEntities[cacheKey] = new CacheItem<T>(entity);
            UpdateIndex(allIndex, entity);
        }

        _cacheLock.EnterWriteLock();
        try
        {
            _localCache = allEntities;
            _index.Clear();
            foreach (var kvp in allIndex)
            {
                _index[kvp.Key] = kvp.Value;
            }
        }
        finally
        {
            if (_cacheLock.IsWriteLockHeld)
            {
                _cacheLock.ExitWriteLock();
            }
        }

        logger.LogInformation("{EntityType} cached and loaded into memory.", typeof(T).Name);
    }

    public async Task<T?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        // Step 1: Check memory cache with timeout
        if (_cacheLock.TryEnterReadLock(LockTimeout))
        {
            try
            {
                if (_localCache.TryGetValue(cacheKey, out var cachedItem))
                {
                    cachedItem.UpdateAccess();
                    logger.LogDebug("Cache hit in memory for {CacheKey}", cacheKey);
                    
                    // Check if cleanup is needed (do this outside the read lock)
                    var shouldCleanup = ShouldTriggerCleanup();
                    if (shouldCleanup)
                    {
                        // Non-blocking cleanup trigger
                        _ = TriggerCleanupAsync();
                    }
                    
                    return cachedItem.Value;
                }
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }
        else
        {
            logger.LogWarning("Failed to acquire read lock for cache key: {CacheKey}. Proceeding without memory cache check.", cacheKey);
        }

        // Step 2: Check distributed cache
        try
        {
            var distributedCacheValue = await cacheResolver().GetAsync<T>(cacheKey, cancellationToken);
            if (distributedCacheValue is not null)
            {
                logger.LogDebug("Cache hit in distributed cache for {CacheKey}", cacheKey);

                // Ensure reference information is properly set after deserialization
                EnsureReferenceIsSet(distributedCacheValue, cacheKey);
                
                // Try to set in local cache without blocking
                _ = TrySetLocalCacheAsync(cacheKey, distributedCacheValue);
                return distributedCacheValue;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to access distributed cache for {CacheKey}. Cache may not be available.", cacheKey);
        }

        // Step 3: Database fallback - load from database and cache
        logger.LogDebug("Cache miss, loading from database for {CacheKey}", cacheKey);
        var databaseValue = await LoadFromDatabaseAsync(cacheKey, cancellationToken);
        if (databaseValue is not null)
        {
            // Ensure reference information is properly set after database load
            EnsureReferenceIsSet(databaseValue, cacheKey);

            // Try to cache the entity without blocking the main flow
            _ = TrySetAsync(databaseValue, cancellationToken);
            logger.LogInformation("Loaded {EntityType} from database and cached: {CacheKey}", typeof(T).Name, cacheKey);
            return databaseValue;
        }

        logger.LogDebug("Entity not found in any cache layer or database for {CacheKey}", cacheKey);
        return null;
    }

    public async Task SetAsync(T entity, CancellationToken cancellationToken = default)
    {
        var cacheKey = entity.CacheKey;

        // Try to set in distributed cache, but don't fail if cache is unavailable
        try
        {
            await cacheResolver().SetAsync(cacheKey, entity, CacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set entity in distributed cache: {CacheKey}. Cache may not be available.", cacheKey);
        }

        // LOCAL CACHE - use timeout to prevent blocking
        if (_cacheLock.TryEnterWriteLock(LockTimeout))
        {
            try
            {
                _localCache[cacheKey] = new CacheItem<T>(entity);
                // INDEX
                UpdateIndex(_index, entity);
                
                // Check if we need to trigger cleanup due to size
                if (_localCache.Count > DefaultCleanupThreshold)
                {
                    logger.LogDebug("Cache size exceeded threshold ({Count} > {Threshold}). Triggering cleanup.", 
                        _localCache.Count, DefaultCleanupThreshold);
                    
                    // Non-blocking cleanup trigger
                    _ = TriggerCleanupAsync();
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }
        else
        {
            logger.LogWarning("Failed to acquire write lock for cache key: {CacheKey}. Local cache not updated.", cacheKey);
        }
    }

    public async Task<List<T>> GetAllByNameAsync(string domain, string name,
        CancellationToken cancellationToken = default)
    {
        List<string>? versions;
        _cacheLock.EnterReadLock();
        try
        {
            if (!_index.TryGetValue(name, out var versionSet))
                return new List<T>();

            versions = versionSet.ToList();
        }
        finally
        {
            if (_cacheLock.IsReadLockHeld)
            {
                _cacheLock.ExitReadLock();
            }
        }

        if (!versions.Any())
            return new List<T>();

        // Create tasks for parallel execution
        var entityTasks = versions.Select(async ver =>
        {
            var cacheKey = CreateCacheKey(domain, name, ver);
            return await GetAsync(cacheKey, cancellationToken);
        });

        // Execute all entity fetching tasks in parallel
        var entityResults = await Task.WhenAll(entityTasks);

        // Filter out null results and return valid entities
        return entityResults.Where(entity => entity != null).ToList()!;
    }

    /// <summary>
    /// Retrieves all entities for the specified domain from the cache.
    /// This method returns the latest version of each entity in the domain.
    /// </summary>
    /// <param name="domain">The domain identifier to retrieve entities for.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A list of all entities for the specified domain.</returns>
    public async Task<List<T>> GetAllByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> latestVersions;

        _cacheLock.EnterReadLock();
        try
        {
            // Get all entities for the domain and find latest version for each key
            latestVersions = _index
                .Where(kvp => kvp.Key.StartsWith($"{domain}:"))
                .ToDictionary(
                    kvp => kvp.Key, // domain:key
                    kvp => kvp.Value.Max ?? string.Empty // latest version
                );
        }
        finally
        {
            if (_cacheLock.IsReadLockHeld)
            {
                _cacheLock.ExitReadLock();
            }
        }

        if (!latestVersions.Any())
            return new List<T>();

        // Create tasks for parallel execution
        var entityTasks = latestVersions
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Select(async kvp =>
            {
                // Extract key from "domain:key" format
                var key = kvp.Key.Substring(domain.Length + 1);
                var cacheKey = CreateCacheKeyWithT(domain, typeof(T).Name switch
                {
                    nameof(Extension) => RuntimeSysSchemaInfo.Extensions,
                    nameof(Definitions.Workflow) => RuntimeSysSchemaInfo.Flows,
                    nameof(WorkflowTask) => RuntimeSysSchemaInfo.Tasks,
                    nameof(Function) => RuntimeSysSchemaInfo.Functions,
                    nameof(View) => RuntimeSysSchemaInfo.Views,
                    nameof(SchemaDefinition) => RuntimeSysSchemaInfo.Schemas,
                    _ => "unknown"
                }, key, kvp.Value);

                return await GetAsync(cacheKey, cancellationToken);
            });

        // Execute all entity fetching tasks in parallel
        var entityResults = await Task.WhenAll(entityTasks);

        // Filter out null results and return valid entities
        return entityResults.Where(entity => entity != null).ToList()!;
    }

    public async Task<T?> GetLatestByNameAsync(
        string domain,
         string flow,
        string name,
        CancellationToken cancellationToken = default)
    {
        string? latestVersion;

        // With timeout acquire ReadLock
        if (_cacheLock.TryEnterReadLock(LockTimeout))
        {
            try
            {
                string indexKey = CreateIndexKey(domain, name);
                if (!_index.TryGetValue(indexKey, out var versionSet) || versionSet.Count == 0)
                {
                    // Release ReadLock, perform database operation
                    _cacheLock.ExitReadLock();
                    
                    // Load from database
                    var databaseEntity = await LoadLatestFromDatabaseAsync(domain, flow, name, cancellationToken);
                    if (databaseEntity != null)
                    {
                        // Non-blocking cache set
                        _ = TrySetAsync(databaseEntity, cancellationToken);
                        return databaseEntity;
                    }
                    return null;
                }
                latestVersion = versionSet.Max;
            }
            finally
            {
                if (_cacheLock.IsReadLockHeld)
                {
                    _cacheLock.ExitReadLock();
                }
            }
        }
        else
        {
            logger.LogWarning("Failed to acquire read lock for GetLatestByNameAsync: {Domain}:{Name}. Loading directly from database.", domain, name);
            // Fallback: Load directly from database
            var databaseEntity = await LoadLatestFromDatabaseAsync(domain, flow, name, cancellationToken);
            if (databaseEntity != null)
            {
                _ = TrySetAsync(databaseEntity, cancellationToken);
                return databaseEntity;
            }
            return null;
        }

        if (latestVersion == null)
            return null;

        var latestCacheKey = CreateCacheKeyWithT(domain, flow, name, latestVersion);
        return await GetAsync(latestCacheKey, cancellationToken);
    }

    public async Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        await cacheResolver().RemoveAsync(cacheKey, cancellationToken);
        InvalidateLocalCache(cacheKey);
    }
    
    /// <summary>
    /// Checks if cleanup should be triggered based on cache size and time
    /// </summary>
    private bool ShouldTriggerCleanup()
    {
        return _localCache.Count > DefaultCleanupThreshold || 
               DateTime.UtcNow - _lastCleanupTime > DefaultCleanupInterval;
    }

    // Non-blocking cleanup trigger, trigger cleanup in background
    private async Task TriggerCleanupAsync()
    {
        if (await _cleanupSemaphore.WaitAsync(0)) // Non-blocking wait
        {
            try
            {
                await Task.Run(() => CleanupExpiredItems());
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }
        else
        {
            logger.LogDebug("Cleanup already in progress, skipping...");
        }
    }

    // Non-blocking cache set
    private async Task TrySetAsync(T entity, CancellationToken cancellationToken)
    {
        try
        {
            await SetAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache entity in background: {CacheKey}", entity.CacheKey);
        }
    }

    // Non-blocking local cache set
    private async Task TrySetLocalCacheAsync(string cacheKey, T entity)
    {
        await Task.Run(() =>
        {
            if (_cacheLock.TryEnterWriteLock(TimeSpan.FromSeconds(1)))
            {
                try
                {
                    _localCache[cacheKey] = new CacheItem<T>(entity);
                    UpdateIndex(_index, entity);
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }
            else
            {
                logger.LogDebug("Could not acquire write lock for local cache update: {CacheKey}", cacheKey);
            }
        });
    }

    /// <summary>
    /// Cleans up expired and least recently used items from the cache
    /// </summary>
    private void CleanupExpiredItems()
    {
        try
        {
            if (_cacheLock.TryEnterWriteLock(LockTimeout))
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var initialCount = _localCache.Count;
                    
                    // Step 1: Remove expired items (based on max age)
                    var expiredKeys = _localCache
                        .Where(kvp => now - kvp.Value.LastAccessTime > DefaultMaxCacheAge)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    
                    foreach (var key in expiredKeys)
                    {
                        if (_localCache.Remove(key, out var removedItem))
                        {
                            RemoveFromIndex(_index, removedItem.Value);
                        }
                    }
                    
                    // Step 2: If still over limit, remove least recently used items
                    if (_localCache.Count > DefaultMaxCacheSize)
                    {
                        var itemsToRemove = _localCache.Count - DefaultMaxCacheSize;
                        var lruKeys = _localCache
                            .OrderBy(kvp => kvp.Value.LastAccessTime)
                            .Take(itemsToRemove)
                            .Select(kvp => kvp.Key)
                            .ToList();
                        
                        foreach (var key in lruKeys)
                        {
                            if (_localCache.Remove(key, out var removedItem))
                            {
                                RemoveFromIndex(_index, removedItem.Value);
                            }
                        }
                    }
                    
                    _lastCleanupTime = now;
                    
                    var finalCount = _localCache.Count;
                    var removedCount = initialCount - finalCount;
                    
                    if (removedCount > 0)
                    {
                        logger.LogInformation("Cache cleanup completed. Removed {RemovedCount} items. Cache size: {InitialCount} -> {FinalCount}", 
                            removedCount, initialCount, finalCount);
                    }
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }
            else
            {
                logger.LogWarning("Failed to acquire write lock for cleanup. Skipping cleanup cycle.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cache cleanup");
        }
    }

    /// <summary>
    /// Loads entity from database using the cache key.
    /// This method provides database fallback when cache miss occurs.
    /// </summary>
    /// <param name="cacheKey">The cache key to parse and load entity from database</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity from database or null if not found</returns>
    private async Task<T?> LoadFromDatabaseAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            // Parse cache key: {EntityType}:{Domain}:{Flow}:{Key}:{Version}
            var match = Regex.Match(cacheKey, @"^([^:]+):([^:]+):([^:]+):([^:]+):([^:]+)$");
            if (!match.Success)
            {
                logger.LogWarning("Invalid cache key format: {CacheKey}", cacheKey);
                return null;
            }

            var domain = match.Groups[2].Value;
            var key = match.Groups[4].Value;
            var version = match.Groups[5].Value;

            return await LoadEntityFromDatabaseAsync(domain, key, version, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading entity from database for cache key: {CacheKey}", cacheKey);
            return null;
        }
    }

    /// <summary>
    /// Loads the latest version of an entity from database.
    /// </summary>
    /// <param name="domain">Domain identifier</param>
    /// <param name="flow">Flow identifier</param>
    /// <param name="key">Entity key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The latest entity from database or null if not found</returns>
    private async Task<T?> LoadLatestFromDatabaseAsync(string domain, string flow, string key, CancellationToken cancellationToken)
    {
        try
        {
            return await LoadEntityFromDatabaseAsync(domain, key, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading latest entity from database for {Domain}:{Flow}:{Key}", domain, flow, key);
            return null;
        }
    }

    /// <summary>
    /// Core method to load entity from database using RuntimeService.
    /// Uses the direct key/version method for efficient database access.
    /// </summary>
    /// <param name="domain">Domain identifier</param>
    /// <param name="key">Entity key</param>
    /// <param name="version">Entity version (null for latest)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity from database or null if not found</returns>
    private async Task<T?> LoadEntityFromDatabaseAsync(string domain, string key, string? version, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
        var runtimeInfoProvider = scope.ServiceProvider.GetRequiredService<IRuntimeInfoProvider>();

        // Validate domain access
        runtimeInfoProvider.Check(domain);

        try
        {
            // If version is specified, use direct key/version method - most efficient
            if (!string.IsNullOrEmpty(version))
            {
                logger.LogDebug("Loading entity from database: {EntityType}:{Domain}:{Key}:{Version}", typeof(T).Name, domain, key, version);
                var entity = await runtimeService.GetAsync<T>(GetSchemaNameForType(), key, version, cancellationToken);

                // Validate the entity belongs to the requested domain
                if (entity != null && entity.Domain == domain)
                {
                    logger.LogDebug("Successfully loaded entity from database: {CacheKey}", entity.CacheKey);
                    return entity;
                }
                logger.LogDebug("Entity not found or domain mismatch: {Domain}:{Key}:{Version}", domain, key, version);
                return null;
            }

            // For latest version, we need to get all entities and find the latest
            // WARNING: This loads ALL entities from schema - use sparingly!
            logger.LogWarning("Loading ALL entities from schema to find latest version - this may impact performance: {EntityType}:{Domain}:{Key}", typeof(T).Name, domain, key);
            var entities = await runtimeService.GetAsync<T>(GetSchemaNameForType(), cancellationToken);

            // Filter by domain and key, then get latest version
            var filteredEntities = entities
                .Where(e => e != null &&
                           e.Domain == domain &&
                           e.Key == key)
                .ToList();

            if (!filteredEntities.Any())
            {
                logger.LogDebug("No entities found for {Domain}:{Key}", domain, key);
                return null;
            }

            // Return latest version based on semantic versioning
            var latestEntity = filteredEntities
                .OrderByDescending(e => e?.Version ?? string.Empty, new SemVersionComparer())
                .FirstOrDefault();

            if (latestEntity != null)
            {
                logger.LogDebug("Found latest entity: {CacheKey}", latestEntity.CacheKey);
            }

            return latestEntity;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading entity from database: Domain={Domain}, Key={Key}, Version={Version}", domain, key, version);
            return null;
        }
    }

    /// <summary>
    /// Gets the schema name for the current entity type.
    /// </summary>
    /// <returns>Schema name for RuntimeService</returns>
    private string GetSchemaNameForType()
    {
        return typeof(T).Name switch
        {
            nameof(Extension) => RuntimeSysSchemaInfo.Extensions,
            nameof(Definitions.Workflow) => RuntimeSysSchemaInfo.Flows,
            nameof(WorkflowTask) => RuntimeSysSchemaInfo.Tasks,
            nameof(Function) => RuntimeSysSchemaInfo.Functions,
            nameof(View) => RuntimeSysSchemaInfo.Views,
            nameof(SchemaDefinition) => RuntimeSysSchemaInfo.Schemas,
            _ => throw new NotSupportedException($"Schema name mapping not found for type: {typeof(T).Name}")
        };
    }

    private void SetLocalCache(string cacheKey, T entity)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            _localCache[cacheKey] = new CacheItem<T>(entity);
            UpdateIndex(_index, entity);
        }
        finally
        {
            if (_cacheLock.IsWriteLockHeld)
            {
                _cacheLock.ExitWriteLock();
            }
        }
    }

    private void InvalidateLocalCache(string cacheKey)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            if (_localCache.Remove(cacheKey, out var removedItem) && removedItem != null)
            {
                RemoveFromIndex(_index, removedItem.Value);
            }
        }
        finally
        {
            if (_cacheLock.IsWriteLockHeld)
            {
                _cacheLock.ExitWriteLock();
            }
        }
    }

    private void ClearLocalCache()
    {
        _cacheLock.EnterWriteLock();
        try
        {
            _localCache.Clear();
            _index.Clear();
        }
        finally
        {
            if (_cacheLock.IsWriteLockHeld)
            {
                _cacheLock.ExitWriteLock();
            }
        }
    }

    private void UpdateIndex(Dictionary<string, SortedSet<string>> index, T entity)
    {
        var indexKey = CreateIndexKey(entity);
        var version = entity.Version;

        if (!index.TryGetValue(indexKey, out var versionSet))
        {
            versionSet = new SortedSet<string>(new SemVersionComparer());
            index[indexKey] = versionSet;
        }

        versionSet.Add(version);
    }

    private void RemoveFromIndex(Dictionary<string, SortedSet<string>> index, T entity)
    {
        var indexKey = CreateIndexKey(entity);
        var version = entity.Version;

        if (index.TryGetValue(indexKey, out var versionSet))
        {
            versionSet.Remove(version);
            if (versionSet.Count == 0)
            {
                index.Remove(indexKey);
            }
        }
    }
    private string CreateCacheKeyWithT(string domain, string flow, string name, string version)
    {
        return $"{typeof(T).Name}:{domain}:{flow}:{name}:{version}";
    }
    private string CreateCacheKey(string domain, string name, string version)
    {
        return $"{domain}:{name}:{version}";
    }

    private string CreateIndexKey(string domain, string name)
    {
        return $"{domain}:{name}";
    }

    private string CreateIndexKey(T entity)
    {
        return $"{entity.Domain}:{entity.Key}";
    }

    /// <summary>
    /// Ensures that the reference information is properly set after deserialization.
    /// This is necessary because some entities may have private setters that don't work during JSON deserialization.
    /// </summary>
    /// <param name="entity">The entity to check and update</param>
    /// <param name="cacheKey">The cache key to parse reference information from</param>
    private void EnsureReferenceIsSet(T entity, string cacheKey)
    {
        // Check if the entity has null reference properties
        if (string.IsNullOrEmpty(entity.Domain) || string.IsNullOrEmpty(entity.Key) || string.IsNullOrEmpty(entity.Version))
        {
            logger.LogDebug("Entity has null reference properties, attempting to set from cache key: {CacheKey}", cacheKey);

            try
            {
                // Parse cache key: {EntityType}:{Domain}:{Flow}:{Key}:{Version}
                // Parse cache key: {EntityType}:{Domain}:{Flow}:{Key}:{Version}
                var match = Regex.Match(cacheKey, @"^([^:]+):([^:]+):([^:]+):([^:]+):([^:]+)$");
                if (match.Success)
                {
                    var domain = match.Groups[2].Value;
                    var flow = match.Groups[3].Value;
                    var key = match.Groups[4].Value;
                    var version = match.Groups[5].Value;

                    // Create reference and set it
                    var reference = new Reference(key, domain, flow, version);
                    entity.SetReference(reference);

                    logger.LogDebug("Successfully set reference for entity: Domain={Domain}, Flow={Flow}, Key={Key}, Version={Version}",
                        domain, flow, key, version);
                }
                else
                {
                    logger.LogWarning("Invalid cache key format for reference setting: {CacheKey}", cacheKey);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting reference for entity from cache key: {CacheKey}", cacheKey);
            }
        }
    }

    public void Dispose()
    {
        _cacheLock.Dispose();
        _cleanupSemaphore.Dispose();
    }
}