using BBT.Workflow.Execution.Notification;
using BBT.Workflow.Infrastructure.Dapr;
using BBT.Workflow.Infrastructure.Dapr.Metadata;
using BBT.Workflow.Infrastructure.Dapr.Notification;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Dapr notification services.
/// </summary>
public static class DaprNotificationServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr notification services including metadata provider, binding resolver,
    /// and warmup hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Requires DaprClient to be registered in the service collection.
    /// Call <c>services.AddDaprClient()</c> before calling this method.
    /// </remarks>
    public static IServiceCollection AddDaprNotification(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options from configuration
        services.Configure<DaprNotificationOptions>(
            configuration.GetSection(DaprNotificationOptions.SectionName));

        // Register core metadata provider (singleton - cached data, uses DaprClient)
        services.AddSingleton<IDaprMetadataProvider, DaprMetadataProvider>();

        // Register notification binding resolver (singleton - lazy resolution)
        services.AddSingleton<INotificationBindingResolver, NotificationBindingResolver>();

        // Register warmup hosted service
        services.AddHostedService<DaprMetadataWarmupHostedService>();

        return services;
    }

    /// <summary>
    /// Adds Dapr notification services with custom options configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure notification options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Requires DaprClient to be registered in the service collection.
    /// Call <c>services.AddDaprClient()</c> before calling this method.
    /// </remarks>
    public static IServiceCollection AddDaprNotification(
        this IServiceCollection services,
        Action<DaprNotificationOptions> configureOptions)
    {
        // Configure options
        services.Configure(configureOptions);

        // Register core metadata provider (singleton - cached data, uses DaprClient)
        services.AddSingleton<IDaprMetadataProvider, DaprMetadataProvider>();

        // Register notification binding resolver (singleton - lazy resolution)
        services.AddSingleton<INotificationBindingResolver, NotificationBindingResolver>();

        // Register warmup hosted service
        services.AddHostedService<DaprMetadataWarmupHostedService>();

        return services;
    }
}
