using BBT.Workflow.Caching;
using BBT.Workflow.Orchestration.Services;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using BBT.Workflow.Tasks.Execution;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions specific to Orchestration API
/// </summary>
public static class OrchestrationApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds Orchestration API specific services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOrchestrationApiModule(this IServiceCollection services)
    {
        // Add Orchestration-specific services
        services
            .AddWorkflowApiBase()
            .AddWorkflowDaprClients()
            .AddWorkflowHttpClient();
            
        // Add health checks separately to avoid ambiguity
        services.AddAppHealthChecks();
        
        // Add Orchestration-specific configurations
        ConfigureOrchestrationSpecificServices(services);
        
        return services;
    }

    private static void ConfigureOrchestrationSpecificServices(IServiceCollection services)
    {
        // Replace default NullTaskExecutor with DaprTaskExecutor for distributed task orchestration
        // DaprTaskExecutor delegates task execution to the Execution service via Dapr Service Invocation
        services.AddScoped<ITaskOrchestrator, DaprTaskExecutor>();
        
        // Add any Orchestration-specific hosted services
        services.AddHostedService<MultiSchemaMigrationHostedService>();
        services.AddHostedService<CacheCleanupHostedService>();
        services.AddHostedService<CacheInitializationHostedService>();
        services.AddHostedService<ScriptingInitializationService>();
    }
} 