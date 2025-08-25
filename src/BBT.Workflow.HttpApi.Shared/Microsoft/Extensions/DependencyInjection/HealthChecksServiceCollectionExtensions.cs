using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for configuring health checks in Workflow API applications
/// </summary>
public static class HealthChecksServiceCollectionExtensions
{
    /// <summary>
    /// Adds standard health checks for Workflow API applications
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        var configuration = services.GetConfiguration();
        var healthChecksBuilder = services.AddHealthChecks().ForwardToPrometheus();

        var endpoint = configuration["Redis:Standalone:EndPoints:0"];
        var password = configuration["Redis:Password"];

        var redisConnectionString = string.IsNullOrWhiteSpace(password)
            ? endpoint
            : $"{endpoint},password={password}";
            
        // Add standard health checks for Workflow APIs
        healthChecksBuilder
            .AddDapr(name: "dapr", tags: ["ready"]) // Dapr sidecar health check
            .AddRedis(redisConnectionString!, name: "redis", tags: ["ready"]) // Redis health check
            .AddNpgSql(configuration.GetConnectionString("Default")!, name: "database", tags: ["ready"]) // PostgreSQL health check
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]); // Self health check
        
        return services;
    }
} 