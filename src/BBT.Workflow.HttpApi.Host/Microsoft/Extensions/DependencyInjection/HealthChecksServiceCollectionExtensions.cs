using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

public static class HealthChecksServiceCollectionExtensions
{
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        var configuration = services.GetConfiguration();
        // Add your health checks here
        var healthChecksBuilder = services.AddHealthChecks();

        var endpoint = configuration["Redis:Standalone:EndPoints:0"];
        var password = configuration["Redis:Password"];

        var redisConnectionString = string.IsNullOrWhiteSpace(password)
            ? endpoint
            : $"{endpoint},password={password}";
        // Other HealthChecks can be added here.
        healthChecksBuilder
            .AddDapr(name: "dapr", tags: ["ready"]) // Optional
            .AddRedis(redisConnectionString!, name: "redis", tags: ["ready"]) // Optional
            .AddNpgSql(configuration.GetConnectionString("Default")!, name: "database", tags: ["ready"]) // Optional
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
        
        return services;
    }
}