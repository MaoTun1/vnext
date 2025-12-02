using BBT.Workflow.Workers.Outbox.HostedServices;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions specific to Worker Outbox
/// </summary>
public static class OutboxWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Adds Worker Outbox specific services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkerOutboxModule(this IServiceCollection services)
    {
        services
            .AddWorkflowApiBase()
            .AddWorkflowDaprClients()
            .AddWorkflowHttpClient();
        
        // Add health checks separately to avoid ambiguity
        services.AddAppHealthChecks();
        
        // Register hosted service
        services.AddHostedService<OutboxProcessorHostedService>();
        
        return services;
    }
}