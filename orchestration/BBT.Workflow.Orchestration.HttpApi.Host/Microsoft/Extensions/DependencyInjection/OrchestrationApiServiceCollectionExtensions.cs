using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Handlers;
using BBT.Workflow.Caching;
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
        // Add IWorkflowTaskExecutor implementation that uses Dapr Service Invocation
        services.AddScoped<ITaskOrchestrator, DaprTaskExecutor>();
        
        // Job Handlers
        services.AddScoped<IJobHandler, FlowTimeoutJobHandler>();
        // services.AddScoped<IJobHandler, TransitionTimerJobHandler>();
        // services.AddScoped<IJobHandler, StartInstanceJobHandler>();
        // services.AddScoped<IJobHandler, TransitionJobHandler>();
        
        // Add any Orchestration-specific hosted services
        services.AddHostedService<CacheInitializationHostedService>();
        services.AddHostedService<ScriptingInitializationService>();
    }
} 