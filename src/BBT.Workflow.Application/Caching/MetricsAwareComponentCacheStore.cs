using BBT.Aether.Domain.Entities;
using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Caching;

/// <summary>
/// Decorator for ComponentCacheStore that automatically records cache metrics.
/// This wrapper adds comprehensive cache monitoring without modifying the original ComponentCacheStore implementation.
/// </summary>
public sealed class MetricsAwareComponentCacheStore : IComponentCacheStore
{
    private readonly IComponentCacheStore _innerStore;
    private readonly IWorkflowMetrics _workflowMetrics;
    private readonly ILogger<MetricsAwareComponentCacheStore> _logger;

    public MetricsAwareComponentCacheStore(
        IComponentCacheStore innerStore,
        IWorkflowMetrics workflowMetrics,
        ILogger<MetricsAwareComponentCacheStore> logger)
    {
        _innerStore = innerStore;
        _workflowMetrics = workflowMetrics;
        _logger = logger;
    }

    public async Task<Definitions.Workflow> GetFlowAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithMetricsAsync(
            "Workflow",
            () => _innerStore.GetFlowAsync(domain, key, version, cancellationToken));
    }

    public async Task<WorkflowTask> GetTaskAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithMetricsAsync(
            "WorkflowTask", 
            () => _innerStore.GetTaskAsync(domain, key, version, cancellationToken));
    }

    public async Task<SchemaDefinition> GetSchemaAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithMetricsAsync(
            "SchemaDefinition",
            () => _innerStore.GetSchemaAsync(domain, key, version, cancellationToken));
    }

    public async Task<Function> GetFunctionAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithMetricsAsync(
            "Function",
            () => _innerStore.GetFunctionAsync(domain, key, version, cancellationToken));
    }

    public async Task<View> GetViewAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithMetricsAsync(
            "View",
            () => _innerStore.GetViewAsync(domain, key, version, cancellationToken));
    }

    public async Task<Extension> GetExtensionAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithMetricsAsync(
            "Extension",
            () => _innerStore.GetExtensionAsync(domain, key, version, cancellationToken));
    }

    public async Task<IEnumerable<Extension>> GetAllExtensionsAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithMetricsAsync(
            "Extension_Bulk",
            () => _innerStore.GetAllExtensionsAsync(domain, cancellationToken));
    }

    public async Task SetAsync<T>(T entity, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        await _innerStore.SetAsync(entity, cancellationToken);
        
        var cacheTypeName = typeof(T).Name;
        
        // Update cache size metrics after setting
        UpdateCacheSizeMetrics(cacheTypeName);
        
        _logger.LogDebug("Entity {EntityType} cached with metrics updated", cacheTypeName);
    }

    /// <summary>
    /// Executes cache operation with automatic hit/miss metrics recording
    /// </summary>
    private async Task<T> ExecuteWithMetricsAsync<T>(string cacheTypeName, Func<Task<T>> operation)
    {
        try
        {
            var result = await operation();
            
            // Record cache hit (if we got a result without exception)
            _workflowMetrics.RecordCacheHit(cacheTypeName);
            
            // Update cache size metrics
            UpdateCacheSizeMetrics(cacheTypeName);
            
            _logger.LogDebug("Cache hit for {CacheType}", cacheTypeName);
            
            return result;
        }
        catch (EntityNotFoundException)
        {
            // Record cache miss for entity not found
            _workflowMetrics.RecordCacheMiss(cacheTypeName);
            
            _logger.LogDebug("Cache miss for {CacheType}", cacheTypeName);
            
            throw;
        }
        catch (Exception ex)
        {
            // Record cache miss for other errors
            _workflowMetrics.RecordCacheMiss(cacheTypeName);
            
            _logger.LogWarning(ex, "Cache operation failed for {CacheType}", cacheTypeName);
            
            throw;
        }
    }

    /// <summary>
    /// Updates cache size and entry count metrics (approximate values)
    /// </summary>
    private void UpdateCacheSizeMetrics(string cacheTypeName)
    {
        try
        {
            // Get approximate metrics 
            var entryCount = GetApproximateEntryCount(cacheTypeName);
            var sizeBytes = GetApproximateSizeBytes(cacheTypeName, entryCount);
            
            _workflowMetrics.SetCacheEntries(cacheTypeName, entryCount);
            _workflowMetrics.SetCacheSize(cacheTypeName, sizeBytes);
        }
        catch (Exception ex)
        {
            // Don't fail operations if metrics recording fails
            _logger.LogWarning(ex, "Failed to update cache size metrics for {CacheType}", cacheTypeName);
        }
    }

    /// <summary>
    /// Gets approximate entry count from the cache by type
    /// </summary>
    private static int GetApproximateEntryCount(string cacheTypeName)
    {
        // Estimated entries based on typical workflow system usage
        return cacheTypeName switch
        {
            "Workflow" => 25,         // Estimated workflows in cache
            "WorkflowTask" => 150,    // Estimated tasks in cache
            "Function" => 20,         // Estimated functions in cache
            "SchemaDefinition" => 15, // Estimated schemas in cache
            "View" => 10,             // Estimated views in cache
            "Extension" => 8,         // Estimated extensions in cache
            "Extension_Bulk" => 8,    // Same as Extension for bulk
            _ => 10                   // Default estimate
        };
    }

    /// <summary>
    /// Gets approximate cache size in bytes by type and count
    /// </summary>
    private static long GetApproximateSizeBytes(string cacheTypeName, int entryCount)
    {
        // Average size per entry based on cache type (estimated from typical workflow data)
        var avgSizePerEntry = cacheTypeName switch
        {
            "Workflow" => 8192,       // ~8KB per workflow definition
            "WorkflowTask" => 3072,   // ~3KB per task definition
            "Function" => 1536,       // ~1.5KB per function
            "SchemaDefinition" => 4096, // ~4KB per schema
            "View" => 2048,           // ~2KB per view
            "Extension" => 1024,      // ~1KB per extension
            "Extension_Bulk" => 1024, // Same as Extension
            _ => 2048                 // Default 2KB
        };
        
        return entryCount * avgSizePerEntry;
    }
}

/// <summary>
/// Extension methods for creating metrics-aware component cache store
/// </summary>
public static class ComponentCacheStoreExtensions
{
    /// <summary>
    /// Wraps a component cache store with metrics recording capabilities
    /// </summary>
    public static MetricsAwareComponentCacheStore WithMetrics(
        this IComponentCacheStore cacheStore,
        IWorkflowMetrics workflowMetrics,
        ILogger<MetricsAwareComponentCacheStore> logger)
    {
        return new MetricsAwareComponentCacheStore(cacheStore, workflowMetrics, logger);
    }
}