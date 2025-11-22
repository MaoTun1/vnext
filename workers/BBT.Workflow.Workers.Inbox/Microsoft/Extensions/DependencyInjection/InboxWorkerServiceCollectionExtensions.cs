using BBT.Workflow.Workers.Inbox.HostedServices;
using BBT.Workflow.Workers.Inbox.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions specific to Worker Inbox
/// </summary>
public static class InboxWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Adds Worker Inbox specific services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkerInboxModule(this IServiceCollection services)
    {
        services
            .AddWorkflowApiBase()
            .AddWorkflowDaprClients();
        
        // Add health checks separately to avoid ambiguity
        services.AddAppHealthChecks();

        // Register multi-schema outbox processor
        services.AddSingleton<IMultiSchemaInboxProcessor, MultiSchemaInboxProcessor>();

        // Register hosted service
        services.AddHostedService<InboxProcessorHostedService>();

        return services;
    }
}