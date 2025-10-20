using BBT.Aether.DistributedCache;
using BBT.Workflow.Definitions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Caching;

public class DomainCacheContext : CacheContext, IDomainCacheContext, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    public ICacheSet<Definitions.Workflow> Workflows { get; }
    public ICacheSet<WorkflowTask> Tasks { get; }
    public ICacheSet<SchemaDefinition> Schemas { get; }
    public ICacheSet<Function> Functions { get; }
    public ICacheSet<View> Views { get; }
    public ICacheSet<Extension> Extensions { get; }

    public DomainCacheContext(IServiceProvider serviceProvider, ILogger<DomainCacheContext> logger)
    {
        _serviceProvider = serviceProvider;
        Workflows = new CacheSet<Definitions.Workflow>(ResolveCacheService, logger, serviceProvider);
        Tasks = new CacheSet<WorkflowTask>(ResolveCacheService, logger, serviceProvider);
        Schemas = new CacheSet<SchemaDefinition>(ResolveCacheService, logger, serviceProvider);
        Functions = new CacheSet<Function>(ResolveCacheService, logger, serviceProvider);
        Views = new CacheSet<View>(ResolveCacheService, logger, serviceProvider);
        Extensions = new CacheSet<Extension>(ResolveCacheService, logger, serviceProvider);

        CacheSets = [Workflows, Tasks, Schemas, Functions, Views,Extensions];
    }

    private IDistributedCacheService ResolveCacheService()
    {
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDistributedCacheService>();
    }

    public void Dispose()
    {
        foreach (var cacheSet in CacheSets)
        {
            cacheSet?.Dispose();
        }
    }
}