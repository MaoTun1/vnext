using BBT.Aether.MultiSchema;
using BBT.Aether.MultiSchema.EntityFrameworkCore.Interceptors;
using BBT.Workflow.Data;
using BBT.Workflow.Infrastructure.DataSink;
using BBT.Workflow.Infrastructure.HostedServices;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Events;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Remote.Extensions;
using BBT.Workflow.Schemas;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up infrastructure services in an <see cref="IServiceCollection" />.
/// </summary>
public static class WorkflowInfrastructureModuleServiceCollectionExtensions
{
    /// <summary>
    /// Adds the infrastructure module services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection AddInfrastructureModule(
        this IServiceCollection services)
    {
        services.AddApplicationModule();
        services.AddAetherInfrastructure();
        
        // DbContext
        services.AddSingleton<NpgsqlSchemaConnectionInterceptor>();
        services.AddScoped<IMultiSchemaMigrator<WorkflowDbContext>, MultiSchemaMigrator<WorkflowDbContext>>();
        
        // You can register your repositories here.
        services.AddScoped<IInstanceRepository, EfCoreInstanceRepository>();
        services.AddScoped<IInstanceCorrelationRepository, EfCoreInstanceCorrelationRepository>();
        services.AddScoped<IInstanceTransitionRepository, EfCoreInstanceTransitionRepository>();
        services.AddScoped<IInstanceTaskRepository, EfCoreInstanceTaskRepository>();
        services.AddScoped<IInstanceJobRepository, EfCoreInstanceJobRepository>();
        
        // Remote vnext api
        services.AddVNextApiServices();
        
        // Monitoring
        services.AddSingleton<IWorkflowMetrics, PrometheusWorkflowMetrics>();
        services.AddSingleton<WorkflowDatabaseInterceptor>();
        services.AddSingleton<WorkflowTransactionInterceptor>();
        
        // Hosted Services
        services.AddHostedService<SystemHealthMonitoringHostedService>();
        
        // DataSink Integration (replaces ClickHouse integration)
        services.AddDataSinkServices();
        services.AddClickHouseDataSinks();
        services.RegisterDataSinks();
        
        // Schema Migration Orchestration
        services.AddScoped<ISchemaMigrationOrchestrator, SchemaMigrationOrchestrator>();
        
        // Event Hooks
        services.AddEventHook<InstanceSubCompletedEvent, InstanceSubCompletedEventHook>();
        
        return services;
    }
}