# Task Factory and Object Pooling System

## Overview

The Task Factory and Object Pooling System provides high-performance task instance creation with memory optimization through object pooling. This system eliminates cache contamination issues while significantly reducing garbage collection pressure in high-throughput scenarios.

## Architecture

### Task Factory Pattern

The `ITaskFactory` interface abstracts task creation and provides a clean separation between cached task definitions and executable task instances.

```csharp
public interface ITaskFactory
{
    /// <summary>
    /// Creates a task instance suitable for execution, ensuring it's isolated from cached instances.
    /// </summary>
    Task<WorkflowTask> CreateExecutionTaskAsync(
        IReference taskReference, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a task instance from cached data with optimization strategies.
    /// </summary>
    WorkflowTask CreateFromCached(WorkflowTask cachedTask);
}
```

### Factory Implementations

#### 1. Standard TaskFactory

Standard implementation using optimized cloning for development and low-throughput scenarios.

```csharp
public sealed class TaskFactory(
    IComponentCacheStore componentCacheStore,
    ILogger<TaskFactory> logger) : ITaskFactory
{
    public async Task<WorkflowTask> CreateExecutionTaskAsync(
        IReference taskReference, 
        CancellationToken cancellationToken = default)
    {
        var cachedTask = await componentCacheStore.GetTaskAsync(taskReference, cancellationToken);
        return CreateFromCached(cachedTask);
    }

    public WorkflowTask CreateFromCached(WorkflowTask cachedTask)
    {
        // Use the task's own optimized Clone method
        var clonedTask = cachedTask.Clone();
        
        logger.LogDebug("Successfully cloned task {TaskKey} of type {TaskType}", 
            cachedTask.Key, cachedTask.GetType().Name);
        
        return clonedTask;
    }
}
```

#### 2. PooledTaskFactory

Advanced implementation with object pooling for high-performance production scenarios.

```csharp
public sealed class PooledTaskFactory(
    IComponentCacheStore componentCacheStore,
    ILogger<PooledTaskFactory> logger,
    IOptions<TaskFactoryOptions> options) : ITaskFactory
{
    private readonly ConcurrentDictionary<Type, ObjectPool<WorkflowTask>> _pools = new();
    private readonly TaskFactoryOptions _options = options.Value;

    public WorkflowTask CreateFromCached(WorkflowTask cachedTask)
    {
        var taskType = cachedTask.GetType();
        
        if (ShouldUsePoolingFromConfig(taskType))
        {
            return CreateFromPool(cachedTask);
        }
        
        return cachedTask.Clone();
    }

    private WorkflowTask CreateFromPool(WorkflowTask template)
    {
        var taskType = template.GetType();
        var pool = _pools.GetOrAdd(taskType, _ => CreatePoolForType(taskType));
        
        var pooledTask = pool.Get();
        
        // Copy properties from template to pooled instance
        CopyTaskProperties(template, pooledTask);
        
        return pooledTask;
    }
}
```

## Object Pooling Registry

### PoolableTaskRegistry

Centralized registry for managing poolable task types and their operations.

```csharp
public static class PoolableTaskRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<WorkflowTask>> Creators = new();
    private static readonly ConcurrentDictionary<Type, Action<WorkflowTask, WorkflowTask>> Copiers = new();

    static PoolableTaskRegistry()
    {
        // Register all poolable task types
        RegisterPoolableTask<DaprServiceTask>(
            DaprServiceTask.CreateEmpty,
            (source, target) => ((DaprServiceTask)target).CopyFromInternal((DaprServiceTask)source));
            
        RegisterPoolableTask<HttpTask>(
            HttpTask.CreateEmpty,
            (source, target) => ((HttpTask)target).CopyFromInternal((HttpTask)source));
            
        // ... other task types
    }

    public static void RegisterPoolableTask<T>(
        Func<WorkflowTask> creator,
        Action<WorkflowTask, WorkflowTask> copier) where T : WorkflowTask
    {
        var type = typeof(T);
        Creators[type] = creator;
        Copiers[type] = copier;
    }

    public static WorkflowTask? TryCreateEmpty(Type taskType)
    {
        return Creators.TryGetValue(taskType, out var creator) ? creator() : null;
    }

    public static bool TryCopyProperties(WorkflowTask source, WorkflowTask target)
    {
        var sourceType = source.GetType();
        if (Copiers.TryGetValue(sourceType, out var copier))
        {
            copier(source, target);
            return true;
        }
        return false;
    }
}
```

## Poolable Task Implementation

### Base Task Support

All poolable tasks require specific methods for object pooling support:

```csharp
public abstract class WorkflowTask : ITaskClonable
{
    // Internal setters for pool operations
    internal void SetKeyInternal(string key) => Key = key;
    internal void SetDomainInternal(string domain) => Domain = domain;
    internal void SetVersionInternal(string version) => Version = version;
    internal void SetConfigInternal(JsonElement config) => Config = config;

    // Pool-friendly copying
    public void CopyBaseToInternal(WorkflowTask target)
    {
        target.SetKeyInternal(Key);
        target.SetDomainInternal(Domain);
        target.SetVersionInternal(Version);
        target.Type = Type;
        target.SetConfigInternal(Config);
    }

    // Reset for pool return
    public virtual void Reset()
    {
        Key = string.Empty;
        Domain = string.Empty;
        Version = string.Empty;
        Type = string.Empty;
        Config = default;
    }

    public abstract WorkflowTask Clone();
}
```

### Task-Specific Implementation Example

```csharp
public sealed class DaprServiceTask : WorkflowTask
{
    // Internal setters for object pooling
    internal void SetAppIdInternal(string appId) => AppId = appId;
    internal void SetMethodNameInternal(string methodName) => MethodName = methodName;
    internal void SetHttpVerbInternal(string httpVerb) => HttpVerb = httpVerb;
    internal void SetDataInternal(JsonElement data) => Data = data;
    internal void SetQueryStringInternal(string? queryString) => QueryString = queryString;
    internal void SetTimeoutSecondsInternal(int timeoutSeconds) => TimeoutSeconds = timeoutSeconds;

    // Efficient internal copying for object pooling
    public void CopyFromInternal(DaprServiceTask source)
    {
        source.CopyBaseToInternal(this);
        SetAppIdInternal(source.AppId);
        SetMethodNameInternal(source.MethodName);
        SetHttpVerbInternal(source.HttpVerb);
        SetDataInternal(source.Data);
        SetQueryStringInternal(source.QueryString);
        SetTimeoutSecondsInternal(source.TimeoutSeconds);
    }

    // Reset for pool return
    public override void Reset()
    {
        base.Reset();
        AppId = string.Empty;
        MethodName = string.Empty;
        HttpVerb = string.Empty;
        Data = default;
        QueryString = null;
        TimeoutSeconds = 30;
    }

    // Empty instance creation for pooling
    public static DaprServiceTask CreateEmpty()
    {
        return new DaprServiceTask();
    }

    // Optimized cloning
    public override WorkflowTask Clone()
    {
        var cloned = new DaprServiceTask();
        CopyBaseTo(cloned);
        
        cloned.AppId = AppId;
        cloned.MethodName = MethodName;
        cloned.HttpVerb = HttpVerb;
        cloned.Data = Data;
        cloned.QueryString = QueryString;
        cloned.TimeoutSeconds = TimeoutSeconds;
        
        return cloned;
    }
}
```

## Configuration

### TaskFactoryOptions

Configuration options for controlling pooling behavior.

```csharp
public sealed class TaskFactoryOptions
{
    [Required]
    public bool UseObjectPooling { get; set; }

    [Range(10, 10000)]
    public int MaxPoolSize { get; set; } = 100;

    [Range(1, 1000)]
    public int InitialPoolSize { get; set; } = 10;

    public bool EnableMetrics { get; set; } = true;

    [Required]
    [MinLength(1)]
    public string[] PooledTaskTypes { get; set; } = Array.Empty<string>();
}
```

### Environment-Specific Configuration

**appsettings.json (Base):**
```json
{
  "TaskFactory": {
    "UseObjectPooling": false,
    "MaxPoolSize": 100,
    "InitialPoolSize": 10,
    "EnableMetrics": true,
    "PooledTaskTypes": [
      "DaprServiceTask",
      "HttpTask",
      "ScriptTask"
    ]
  }
}
```

**appsettings.Dev.json:**
```json
{
  "TaskFactory": {
    "UseObjectPooling": false,
    "MaxPoolSize": 20,
    "InitialPoolSize": 2,
    "EnableMetrics": true
  }
}
```

**appsettings.Prod.json:**
```json
{
  "TaskFactory": {
    "UseObjectPooling": true,
    "MaxPoolSize": 1000,
    "InitialPoolSize": 100,
    "EnableMetrics": false,
    "PooledTaskTypes": [
      "DaprServiceTask",
      "HttpTask",
      "ScriptTask",
      "ConditionTask",
      "DaprBindingTask",
      "DaprHttpEndpointTask",
      "DaprPubSubTask",
      "HumanTask"
    ]
  }
}
```

## Performance Metrics and Sizing

### TaskFactoryMetrics

Utility class for calculating optimal pool sizes and memory usage.

```csharp
public static class TaskFactoryMetrics
{
    public static long CalculateApproximateMemoryPerTask(Type taskType)
    {
        return taskType.Name switch
        {
            "DaprServiceTask" => 2048,      // ~2KB
            "HttpTask" => 3072,             // ~3KB
            "ScriptTask" => 1024,           // ~1KB
            "DaprHttpEndpointTask" => 2048, // ~2KB
            "DaprBindingTask" => 1536,      // ~1.5KB
            "DaprPubSubTask" => 1536,       // ~1.5KB
            "HumanTask" => 4096,            // ~4KB
            _ => 1024                       // Default ~1KB
        };
    }

    public static PoolSizingRecommendation CalculateOptimalPoolSizes(
        int maxConcurrentRequests,
        int taskExecutionRatePerSecond, 
        int averageTaskLifetimeMs,
        int availableMemoryMB)
    {
        var concurrentTasks = (taskExecutionRatePerSecond * averageTaskLifetimeMs) / 1000.0;
        var bufferedConcurrentTasks = (int)(concurrentTasks * 1.2); // 20% safety margin
        var requestBasedPoolSize = Math.Max(maxConcurrentRequests / 2, 10);
        var recommendedMaxSize = Math.Max(bufferedConcurrentTasks, requestBasedPoolSize);
        var memoryLimitedSize = CalculateMemoryConstrainedPoolSize(availableMemoryMB);
        
        recommendedMaxSize = Math.Min(recommendedMaxSize, memoryLimitedSize);
        var recommendedInitialSize = Math.Max(recommendedMaxSize / 10, 5);
        
        return new PoolSizingRecommendation
        {
            RecommendedMaxPoolSize = recommendedMaxSize,
            RecommendedInitialPoolSize = recommendedInitialSize,
            EstimatedMemoryUsageMB = CalculateEstimatedMemoryUsage(recommendedMaxSize),
            ConcurrentTasksCalculated = (int)concurrentTasks,
            MemoryConstrainedSize = memoryLimitedSize
        };
    }
}
```

## Cache Contamination Prevention

### Problem Solved

Before the Task Factory system, tasks were shared by reference from cache:

```csharp
// PROBLEMATIC (Old Approach)
var task = await cache.GetTaskAsync(reference); // Shared reference
task.SetMethodName(newMethodName); // Modifies cached instance!
// Next request gets contaminated task
```

### Solution Implementation

With Task Factory, instances are properly isolated:

```csharp
// SAFE (New Approach)
var task = await taskFactory.CreateExecutionTaskAsync(reference); // Isolated instance
task.SetMethodName(newMethodName); // Only affects this instance
// Next request gets fresh task from cache
```

## Best Practices

### 1. Configuration Guidelines

- **Development**: Disable pooling for easier debugging
- **Staging**: Enable pooling with small pool sizes for testing
- **Production**: Enable pooling with optimized sizes based on load

### 2. Pool Sizing

```csharp
// Calculate optimal pool size
var recommendation = TaskFactoryMetrics.CalculateOptimalPoolSizes(
    maxConcurrentRequests: 500,
    taskExecutionRatePerSecond: 100,
    averageTaskLifetimeMs: 200,
    availableMemoryMB: 4096);
    
// Apply recommendation to configuration
options.MaxPoolSize = recommendation.RecommendedMaxPoolSize;
options.InitialPoolSize = recommendation.RecommendedInitialPoolSize;
```

### 3. Memory Monitoring

```csharp
// Monitor pool usage
var validation = TaskFactoryMetrics.ValidatePoolConfiguration(options, availableMemoryMB);
if (validation.MemoryUsagePercentage > 15)
{
    logger.LogWarning("Pool configuration uses {MemoryPercentage}% of available memory", 
        validation.MemoryUsagePercentage);
}
```

### 4. Error Handling

```csharp
public async Task<WorkflowTask> CreateExecutionTaskAsync(
    IReference taskReference, 
    CancellationToken cancellationToken = default)
{
    try
    {
        var cachedTask = await componentCacheStore.GetTaskAsync(taskReference, cancellationToken);
        return CreateFromCached(cachedTask);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create execution task for reference: {TaskReference}", taskReference);
        throw;
    }
}
```

## Monitoring and Diagnostics

### Metrics Collection

```csharp
// Track pool usage metrics
services.AddSingleton<ITaskFactoryMetrics, TaskFactoryMetrics>();

// Example metrics
- task_factory_pool_size_current
- task_factory_pool_size_max  
- task_factory_created_total
- task_factory_returned_total
- task_factory_cache_hits_total
- task_factory_cache_misses_total
```

### Adding New Poolable Task Types

1. **Implement pooling methods in task class:**
```csharp
public sealed class NewTaskType : WorkflowTask
{
    // Add internal setters
    internal void SetPropertyInternal(string value) => Property = value;
    
    // Add CopyFromInternal method
    public void CopyFromInternal(NewTaskType source) { /* implementation */ }
    
    // Override Reset method
    public override void Reset() { /* implementation */ }
    
    // Add CreateEmpty method
    public static NewTaskType CreateEmpty() => new();
}
```

2. **Register in PoolableTaskRegistry:**
```csharp
RegisterPoolableTask<NewTaskType>(
    NewTaskType.CreateEmpty,
    (source, target) => ((NewTaskType)target).CopyFromInternal((NewTaskType)source));
```

3. **Add to configuration:**
```json
{
  "TaskFactory": {
    "PooledTaskTypes": [
      "NewTaskType"
    ]
  }
}
```

## Dependency Injection Setup

### Automatic Configuration-Driven Registration

The system automatically selects the appropriate factory implementation based on configuration:

```csharp
private static void ConfigureTaskFactory(IServiceCollection services)
{
    // Configure options with validation
    services.AddOptions<TaskFactoryOptions>()
        .BindConfiguration(TaskFactoryOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Register TaskFactory implementation based on configuration
    services.AddScoped<ITaskFactory>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<TaskFactoryOptions>>();
        var componentCacheStore = serviceProvider.GetRequiredService<IComponentCacheStore>();
        
        if (options.Value.UseObjectPooling)
        {
            // Get singleton PooledTaskFactory for object pooling efficiency
            return serviceProvider.GetRequiredService<PooledTaskFactory>();
        }

        var standardLogger = serviceProvider.GetRequiredService<ILogger<TaskFactory>>();
        return new TaskFactory(componentCacheStore, standardLogger);
    });

    // Register standard TaskFactory as scoped (stateless)
    services.AddScoped<TaskFactory>();
    
    // Register PooledTaskFactory as SINGLETON for efficient object pooling
    services.AddSingleton<PooledTaskFactory>();
}
```

### DI Lifecycle Best Practices

| Component | Lifecycle | Rationale |
|-----------|-----------|-----------|
| `ITaskFactory` | **Scoped** | Per-request dependency injection |
| `TaskFactory` | **Scoped** | Stateless service, request isolation |
| `PooledTaskFactory` | **Singleton** | Shared pools across requests for efficiency |
| `IComponentCacheStore` | **Singleton** | Thread-safe caching for performance |

### Why Singleton for PooledTaskFactory?

```csharp
// ✅ CORRECT: Singleton ensures shared pools
services.AddSingleton<PooledTaskFactory>();

// Benefits:
// - Object pools shared across all HTTP requests
// - Maximum memory efficiency 
// - High object reuse rates
// - Thread-safe pool implementations

// ❌ WRONG: Scoped would break pooling
services.AddScoped<PooledTaskFactory>(); 

// Problems:
// - New pools created per request
// - Memory waste
// - No pool sharing benefits
// - Defeats pooling purpose
```

### Development vs Production Registration

**Development Environment:**
```csharp
// appsettings.Development.json
{
  "TaskFactory": {
    "UseObjectPooling": false
  }
}

// Result: Uses scoped TaskFactory for easier debugging
services.AddScoped<ITaskFactory, TaskFactory>();
```

**Production Environment:**
```csharp
// appsettings.Production.json  
{
  "TaskFactory": {
    "UseObjectPooling": true
  }
}

// Result: Uses singleton PooledTaskFactory for performance
services.AddScoped<ITaskFactory>(sp => sp.GetRequiredService<PooledTaskFactory>());
services.AddSingleton<PooledTaskFactory>();
``` 

The Task Factory and Object Pooling System provides a robust, high-performance solution for task instance management while maintaining clean architecture principles and ensuring cache safety in high-throughput environments. 