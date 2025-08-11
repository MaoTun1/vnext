using BBT.Workflow.Definitions;
using BBT.Workflow.Runtime;
using Nito.Disposables;

namespace BBT.Workflow.Caching;

/// <summary>
/// Hosted service responsible for initializing cache components during application startup.
/// This service loads workflow definitions, tasks, functions, views, schemas, and extensions
/// into the domain cache context for improved runtime performance.
/// </summary>
/// <param name="serviceProvider">Service provider for resolving dependencies</param>
public class CacheInitializationHostedService(
    IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var flows = await GetComponentAsync<Definitions.Workflow>(RuntimeSysSchemaInfo.Flows, cancellationToken);
        var tasks = await GetComponentAsync<WorkflowTask>(RuntimeSysSchemaInfo.Tasks, cancellationToken);
        var functions = await GetComponentAsync<Function>(RuntimeSysSchemaInfo.Functions, cancellationToken);
        var views = await GetComponentAsync<View>(RuntimeSysSchemaInfo.Views, cancellationToken);
        var schemas = await GetComponentAsync<SchemaDefinition>(RuntimeSysSchemaInfo.Schemas, cancellationToken);
        var extensions = await GetComponentAsync<Extension>(RuntimeSysSchemaInfo.Extensions, cancellationToken);

        var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DomainCacheContext>();

        var initialData = new Dictionary<Type, object>
        {
            { typeof(Definitions.Workflow), flows },
            { typeof(WorkflowTask), tasks },
            { typeof(SchemaDefinition), schemas },
            { typeof(Function), functions },
            { typeof(View), views },
            { typeof(Extension), extensions }
        };

        await context.InitializeAsync(initialData, cancellationToken);
        scope.ToAsyncDisposable();
    }

    /// <summary>
    /// Retrieves components of a specific type from the runtime service.
    /// </summary>
    /// <typeparam name="T">The type of domain entity to retrieve, must implement IDomainEntity and IReferenceSetter</typeparam>
    /// <param name="name">The schema name to retrieve components from</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A collection of retrieved components of type T</returns>
    private async Task<IEnumerable<T?>> GetComponentAsync<T>(string name, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var scope = serviceProvider.CreateAsyncScope();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();

        var items = await runtimeService.GetAsync<T>(name, cancellationToken);
        scope.ToAsyncDisposable();
        return items;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}