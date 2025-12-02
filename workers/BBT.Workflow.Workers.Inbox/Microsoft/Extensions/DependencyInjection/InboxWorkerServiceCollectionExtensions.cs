using BBT.Workflow.Workers.Inbox.HostedServices;

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
            .AddWorkflowDaprClients()
            .AddWorkflowHttpClient();
        
        // Add health checks separately to avoid ambiguity
        services.AddAppHealthChecks();
        
        // Register hosted service
        services.AddHostedService<InboxProcessorHostedService>();

        return services;
    }
}