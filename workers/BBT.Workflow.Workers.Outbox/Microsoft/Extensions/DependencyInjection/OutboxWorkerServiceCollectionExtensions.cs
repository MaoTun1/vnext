using BBT.Workflow.Workers.Outbox.HostedServices;
using BBT.Workflow.Workers.Outbox.Services;

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
            .AddWorkflowDaprClients();
        
        // Add health checks separately to avoid ambiguity
        services.AddAppHealthChecks();

        // Register multi-schema outbox processor
        services.AddSingleton<IMultiSchemaOutboxProcessor, MultiSchemaOutboxProcessor>();

        // Register hosted service
        services.AddHostedService<OutboxProcessorHostedService>();
        
        return services;
    }
}