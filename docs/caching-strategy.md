# Caching Strategy

## Overview

The BBT Workflow Engine implements a sophisticated multi-level caching strategy to optimize performance for workflow definitions, instances, and domain data. The caching system combines local in-memory caching with distributed Redis caching to provide high-performance data access while maintaining consistency across multiple application instances.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                Application Layer                        │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │  IComponentCache│  │ IInstanceCache  │             │
│  │      Store      │  │     Store       │             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│              Multi-Level Cache System                   │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │   Local Cache   │  │ Distributed     │             │
│  │  (In-Memory)    │  │  Cache (Redis)  │             │
│  │                 │  │                 │             │
│  │ • Fast Access   │  │ • Shared State  │             │
│  │ • Thread-Safe   │  │ • Persistence   │             │
│  │ • Indexed       │  │ • Scalability   │             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│                Database Layer                           │
│              (PostgreSQL)                               │
└─────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Domain Cache Context

The central orchestrator for all workflow-related caching:

```csharp
public class DomainCacheContext : CacheContext
{
    public CacheSet<Workflow> Workflows { get; }
    public CacheSet<WorkflowTask> Tasks { get; }
    public CacheSet<SchemaDefinition> Schemas { get; }
    public CacheSet<Function> Functions { get; }
    public CacheSet<View> Views { get; }
    public CacheSet<Extension> Extensions { get; }

    public DomainCacheContext(IServiceProvider serviceProvider, ILogger<DomainCacheContext> logger)
    {
        Workflows = new CacheSet<Workflow>(ResolveCacheService, logger);
        Tasks = new CacheSet<WorkflowTask>(ResolveCacheService, logger);
        Schemas = new CacheSet<SchemaDefinition>(ResolveCacheService, logger);
        Functions = new CacheSet<Function>(ResolveCacheService, logger);
        Views = new CacheSet<View>(ResolveCacheService, logger);
        Extensions = new CacheSet<Extension>(ResolveCacheService, logger);
    }
}
```

### 2. CacheSet<T> Implementation

Provides thread-safe, dual-layer caching for domain entities:

```csharp
public class CacheSet<T> where T : class, IDomainEntity
{
    private readonly ReaderWriterLockSlim _cacheLock = new();
    private Dictionary<string, T> _localCache = new();
    private readonly Dictionary<string, SortedSet<string>> _index = new();

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),
    };
}
```

### 3. Component Cache Store

Provides high-level API for accessing cached workflow components:

```csharp
public interface IComponentCacheStore
{
    Task<Workflow> GetFlowAsync(string domain, string key, string? version, CancellationToken cancellationToken = default);
    Task<WorkflowTask> GetTaskAsync(string domain, string key, string? version, CancellationToken cancellationToken = default);
    Task<SchemaDefinition> GetSchemaAsync(string domain, string key, string? version, CancellationToken cancellationToken = default);
    Task<Function> GetFunctionAsync(string domain, string key, string? version, CancellationToken cancellationToken = default);
    Task<View> GetViewAsync(string domain, string key, string? version, CancellationToken cancellationToken = default);
    Task<Extension> GetExtensionAsync(string domain, string key, string? version, CancellationToken cancellationToken = default);
    Task SetAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IDomainEntity;
}
```

## Cache Key Strategy

### 1. Entity Cache Keys

Each domain entity implements a consistent cache key format:

```csharp
public interface IDomainEntity : IHasKey, IHasVersion, IHasDomain
{
    string CacheKey { get; }
}

// Example implementations
public class Workflow
{
    public string CacheKey => $"{nameof(Workflow)}:{Domain}:{Flow}:{Key}:{Version}";
}

public class WorkflowTask
{
    public string CacheKey => $"{nameof(WorkflowTask)}:{Domain}:{Flow}:{Key}:{Version}";
}
```

### 2. Version-Based Indexing

The system maintains version indexes for efficient latest version retrieval:

```csharp
private void UpdateIndex(Dictionary<string, SortedSet<string>> index, T entity)
{
    var indexKey = CreateIndexKey(entity); // "domain:key"
    var version = entity.Version;

    if (!index.TryGetValue(indexKey, out var versionSet))
    {
        versionSet = new SortedSet<string>(new SemVersionComparer());
        index[indexKey] = versionSet;
    }

    versionSet.Add(version);
}
```

### 3. Schema-Aware Cache Keys

For multi-schema scenarios, cache keys include schema information:

```csharp
public class CoreModelCacheKey : ModelCacheKey
{
    readonly string? _schema = (context as WorkflowDbContext)?.SchemaName ?? "public";
    
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), _schema);
    }
}
```

## Caching Layers

### 1. Local In-Memory Cache

**Purpose**: Ultra-fast access for frequently accessed data
**Implementation**: Thread-safe dictionary with ReaderWriterLockSlim
**Scope**: Single application instance

```csharp
public async Task<T?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
{
    // First, check local cache
    _cacheLock.EnterReadLock();
    try
    {
        if (_localCache.TryGetValue(cacheKey, out var cachedValue))
        {
            return cachedValue;
        }
    }
    finally
    {
        _cacheLock.ExitReadLock();
    }

    // Fall back to distributed cache
    var distributedCacheValue = await cacheResolver().GetAsync<T>(cacheKey, cancellationToken);
    if (distributedCacheValue is not null)
    {
        SetLocalCache(cacheKey, distributedCacheValue);
    }

    return distributedCacheValue;
}
```

### 2. Distributed Redis Cache

**Purpose**: Shared state across application instances
**Implementation**: Redis with configurable options
**Scope**: All application instances

```csharp
public async Task SetAsync(T entity, CancellationToken cancellationToken = default)
{
    var cacheKey = entity.CacheKey;
    
    // Store in distributed cache first
    await cacheResolver().SetAsync(cacheKey, entity, CacheOptions, cancellationToken);
    
    // Update local cache
    _cacheLock.EnterWriteLock();
    try
    {
        _localCache[cacheKey] = entity;
        UpdateIndex(_index, entity);
    }
    finally
    {
        _cacheLock.ExitWriteLock();
    }
}
```

## Redis Configuration

### 1. Standalone Configuration

```json
{
  "Redis": {
    "Mode": "Standalone",
    "InstanceName": "workflow-api",
    "ConnectionTimeout": 5000,
    "DefaultDatabase": 0,
    "Password": "",
    "Ssl": false,
    "Standalone": {
      "EndPoints": ["localhost:6379"]
    },
    "RetryPolicy": {
      "MaxRetries": 3,
      "RetryTimeout": 1000
    }
  }
}
```

### 2. Cluster Configuration

```json
{
  "Redis": {
    "Mode": "Cluster",
    "InstanceName": "workflow-api",
    "ConnectionTimeout": 5000,
    "DefaultDatabase": 0,
    "Password": "ENV_REDIS_PASSWORD",
    "Ssl": false,
    "Cluster": {
      "EndPoints": [
        "redis-cluster-1.example.com:6379",
        "redis-cluster-2.example.com:6379",
        "redis-cluster-3.example.com:6379"
      ]
    }
  }
}
```

## Cache Initialization

### 1. Hosted Service for Cache Warm-up

```csharp
public class CacheInitializationHostedService : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Load all system components into cache
        var flows = await GetComponentAsync<Workflow>(RuntimeSysSchemaInfo.Flows, cancellationToken);
        var tasks = await GetComponentAsync<WorkflowTask>(RuntimeSysSchemaInfo.Tasks, cancellationToken);
        var functions = await GetComponentAsync<Function>(RuntimeSysSchemaInfo.Functions, cancellationToken);
        var views = await GetComponentAsync<View>(RuntimeSysSchemaInfo.Views, cancellationToken);
        var schemas = await GetComponentAsync<SchemaDefinition>(RuntimeSysSchemaInfo.Schemas, cancellationToken);
        var extensions = await GetComponentAsync<Extension>(RuntimeSysSchemaInfo.Extensions, cancellationToken);

        var initialData = new Dictionary<Type, object>
        {
            { typeof(Workflow), flows },
            { typeof(WorkflowTask), tasks },
            { typeof(SchemaDefinition), schemas },
            { typeof(Function), functions },
            { typeof(View), views },
            { typeof(Extension), extensions }
        };

        await context.InitializeAsync(initialData, cancellationToken);
    }
}
```

### 2. Bulk Loading

```csharp
public async Task LoadAllAsync(object data, CancellationToken cancellationToken = default)
{
    if (data is not IEnumerable<T> entities)
        throw new ArgumentException($"Invalid data type for {typeof(T).Name}");

    var allEntities = new Dictionary<string, T>();
    var allIndex = new Dictionary<string, SortedSet<string>>();
    
    foreach (var entity in entities)
    {
        var cacheKey = entity.CacheKey;
        
        // Store in distributed cache
        await cacheResolver().SetAsync(cacheKey, entity, CacheOptions, cancellationToken);
        
        // Store in local cache
        allEntities[cacheKey] = entity;
        UpdateIndex(allIndex, entity);
    }

    // Update local cache atomically
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
        _cacheLock.ExitWriteLock();
    }
}
```

## Version Management

### 1. Semantic Version Comparison

```csharp
public class SemVersionComparer : IComparer<string>
{
    public int Compare(string x, string y)
    {
        if (Version.TryParse(x, out var versionX) && Version.TryParse(y, out var versionY))
        {
            return versionX.CompareTo(versionY);
        }
        
        return string.Compare(x, y, StringComparison.Ordinal);
    }
}
```

### 2. Latest Version Retrieval

```csharp
public async Task<T?> GetLatestByNameAsync(
    string domain, 
    string flow, 
    string name,
    CancellationToken cancellationToken = default)
{
    string? latestVersion;

    _cacheLock.EnterReadLock();
    try
    {
        string indexKey = CreateIndexKey(domain, name);
        if (!_index.TryGetValue(indexKey, out var versionSet) || versionSet.Count == 0)
        {
            return null;
        }
        latestVersion = versionSet.Max; // Highest version due to SemVersionComparer
    }
    finally
    {
        _cacheLock.ExitReadLock();
    }

    var latestCacheKey = CreateCacheKeyWithT(domain, flow, name, latestVersion);
    return await GetAsync(latestCacheKey, cancellationToken);
}
```

## Cache Invalidation

### 1. Manual Invalidation

```csharp
public async Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
{
    // Remove from distributed cache
    await cacheResolver().RemoveAsync(cacheKey, cancellationToken);
    
    // Remove from local cache
    InvalidateLocalCache(cacheKey);
}

private void InvalidateLocalCache(string cacheKey)
{
    _cacheLock.EnterWriteLock();
    try
    {
        if (_localCache.Remove(cacheKey, out var removedEntity) && removedEntity != null)
        {
            RemoveFromIndex(_index, removedEntity);
        }
    }
    finally
    {
        _cacheLock.ExitWriteLock();
    }
}
```

### 2. Time-Based Expiration

```csharp
private static readonly DistributedCacheEntryOptions CacheOptions = new()
{
    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1),
    SlidingExpiration = TimeSpan.FromMinutes(30)
};
```

### 3. Admin API for Cache Management

```csharp
public async Task InvalidateCacheAsync(InvalidateCacheInput input, CancellationToken cancellationToken = default)
{
    using (currentSchema.Change(input.Flow))
    {
        var instance = await instanceRepository.FindByKeyAsync(input.Key, cancellationToken);
        if (instance?.LatestData == null)
        {
            throw new EntityNotFoundException(typeof(Instance), input.Key);
        }

        // Reprocess and cache the component
        await castProcessor.ProcessAsync(
            input.Flow,
            new Reference(input.Key, input.Domain, input.Flow, input.Version ?? instance.LatestData.Version),
            instance.LatestData.Data.JsonElement,
            cancellationToken);
    }
}
```

## Performance Optimizations

### 1. Connection Pooling

```csharp
services.AddHttpClient<HttpTaskExecutor>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    MaxConnectionsPerServer = 10
});
```

### 2. Lazy Loading and Initialization

```csharp
private static readonly Lazy<MetadataReference[]> DefaultReferences = new(() => new[]
{
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    MetadataReference.CreateFromFile(typeof(IMapping).Assembly.Location),
    // ... other references
});
```

### 3. Optimized Serialization

```csharp
public static class JsonSerializerConstants
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

## Multi-Schema Cache Support

### 1. Schema-Aware Model Caching

```csharp
public class DynamicSchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => new CoreModelCacheKey(context, designTime);
}

class CoreModelCacheKey : ModelCacheKey
{
    readonly string? _schema = (context as WorkflowDbContext)?.SchemaName ?? "public";
    
    protected override bool Equals(ModelCacheKey other)
    {
        return base.Equals(other)
               && other is CoreModelCacheKey otherKey
               && otherKey._schema == _schema;
    }
}
```

### 2. Schema Context Management

```csharp
public async Task<T> GetAsync<T>(
    string domain,
    string flow,
    string key,
    string? version,
    CancellationToken cancellationToken = default)
    where T : class, IDomainEntity
{
    var cacheSet = GetCacheSet<T>();

    var entity = version.IsNullOrEmpty()
        ? await cacheSet.GetLatestByNameAsync(domain, flow, key, cancellationToken)
        : await cacheSet.GetAsync($"{typeof(T).Name}:{domain}:{flow}:{key}:{version}", cancellationToken);

    if (entity == null)
    {
        throw new EntityNotFoundException(typeof(T), new { domain, key, version });
    }

    return entity;
}
```

## Monitoring and Diagnostics

### 1. Cache Performance Metrics

```csharp
public class CacheSet<T>
{
    private readonly ILogger _logger;
    
    public async Task<T?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Check local cache first
        var localResult = GetFromLocalCache(cacheKey);
        if (localResult != null)
        {
            _logger.LogDebug("Cache hit (local): {CacheKey} in {ElapsedMs}ms", 
                cacheKey, stopwatch.ElapsedMilliseconds);
            return localResult;
        }
        
        // Check distributed cache
        var distributedResult = await GetFromDistributedCache(cacheKey, cancellationToken);
        if (distributedResult != null)
        {
            _logger.LogDebug("Cache hit (distributed): {CacheKey} in {ElapsedMs}ms", 
                cacheKey, stopwatch.ElapsedMilliseconds);
            return distributedResult;
        }
        
        _logger.LogDebug("Cache miss: {CacheKey} in {ElapsedMs}ms", 
            cacheKey, stopwatch.ElapsedMilliseconds);
        return null;
    }
}
```

### 2. Health Checks

```csharp
public class CacheHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Test Redis connectivity
            await _distributedCache.SetStringAsync("health_check", "ok", cancellationToken);
            var result = await _distributedCache.GetStringAsync("health_check", cancellationToken);
            
            return result == "ok" 
                ? HealthCheckResult.Healthy("Cache is operational")
                : HealthCheckResult.Unhealthy("Cache test failed");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cache is unavailable", ex);
        }
    }
}
```

## Usage Examples

### 1. Retrieving Workflow Definitions

```csharp
// Get latest version
var workflow = await componentCache.GetFlowAsync("banking", "loan-approval", null, cancellationToken);

// Get specific version
var workflowV2 = await componentCache.GetFlowAsync("banking", "loan-approval", "2.1.0", cancellationToken);
```

### 2. Caching Custom Data

```csharp
// Store in cache
var workflow = new Workflow { Domain = "banking", Key = "loan-approval", Version = "1.0.0" };
await componentCache.SetAsync(workflow, cancellationToken);

// Retrieve from cache
var cached = await componentCache.GetFlowAsync("banking", "loan-approval", "1.0.0", cancellationToken);
```

### 3. Bulk Operations

```csharp
// Load multiple workflows
var workflows = new List<Workflow> { workflow1, workflow2, workflow3 };
await cacheContext.Workflows.LoadAllAsync(workflows, cancellationToken);
```

## Best Practices

### 1. Cache Key Design
- Use consistent naming conventions
- Include all identifying information (domain, key, version)
- Keep keys readable for debugging

### 2. Expiration Strategy
- Set appropriate TTL based on data volatility
- Use sliding expiration for frequently accessed data
- Implement cache warming for critical data

### 3. Error Handling
- Always have fallback to database when cache fails
- Log cache misses and performance metrics
- Implement circuit breaker patterns for Redis connectivity

### 4. Memory Management
- Monitor local cache size
- Implement LRU eviction if needed
- Use weak references for large objects

The caching strategy provides excellent performance while maintaining data consistency across the distributed workflow engine, enabling high-throughput workflow processing with minimal database load. 