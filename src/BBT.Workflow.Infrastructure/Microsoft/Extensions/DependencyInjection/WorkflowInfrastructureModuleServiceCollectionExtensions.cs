using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Data;
using BBT.Workflow.Instances;
using BBT.Workflow.Remote.Extensions;
using BBT.Workflow.Schemas;
using Microsoft.EntityFrameworkCore;

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

        // Schemas
        services.AddScoped<ISchemaManager, PostgresSchemaManager>();
        
        // Background Jobs
        services.AddScoped<IBackgroundJobService, DaprBackgroundJobService>();
        services.AddScoped<IJobStore, EfCoreJobStore>();
        
        // DbContext
        services.AddScoped<IDbContextFactory<WorkflowDbContext>, WorkflowDbContextFactory>();

        // You can register your repositories here.
        services.AddScoped<IInstanceRepository, EfCoreInstanceRepository>();
        services.AddScoped<IInstanceCorrelationRepository, EfCoreInstanceCorrelationRepository>();
        services.AddScoped<IInstanceTransitionRepository, EfCoreInstanceTransitionRepository>();
        services.AddScoped<IInstanceTaskRepository, EfCoreInstanceTaskRepository>();
        services.AddScoped<IInstanceJobRepository, EfCoreInstanceJobRepository>();
        
        // Remote vnext api
        services.AddVNextApiServices();
        
        return services;
    }
}