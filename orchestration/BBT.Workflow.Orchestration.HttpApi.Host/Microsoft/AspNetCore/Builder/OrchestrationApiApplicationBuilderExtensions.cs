using BBT.Workflow.BackgroundJobs;
using Dapr.Jobs.Extensions;
using Prometheus;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Application builder extensions specific to Orchestration API
/// </summary>
public static class OrchestrationApiApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the Orchestration API application pipeline
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication UseOrchestrationApiModule(this WebApplication app)
    {
        // Use base Workflow API configuration (already includes UseHttpMetrics and MapMetrics)
        app.UseWorkflowApiBase();
        app.MapAppHealthChecks();
        
        // Add Orchestration-specific middleware and configurations
        ConfigureOrchestrationSpecificMiddleware(app);

        // Add Dapr scheduled job handler (Orchestration-specific)
        app.MapDaprScheduledJobHandler(async (string jobName, ReadOnlyMemory<byte> jobPayload, JobDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            await dispatcher.DispatchAsync(jobName, jobPayload, cancellationToken);
        });

        // Seed test data
        WorkflowApiBaseApplicationBuilderExtensions.SeedTestData(app.Services);

        return app;
    }

    private static void ConfigureOrchestrationSpecificMiddleware(WebApplication app)
    {
        // Add any Orchestration-specific middleware here
    }
}
 