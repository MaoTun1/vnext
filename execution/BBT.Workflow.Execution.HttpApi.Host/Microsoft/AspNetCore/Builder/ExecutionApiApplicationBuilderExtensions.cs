using BBT.Workflow.BackgroundJobs;
using Dapr.Jobs.Extensions;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Application builder extensions specific to Execution API
/// </summary>
public static class ExecutionApiApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the Execution API application pipeline
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication UseExecutionApiModule(this WebApplication app)
    {
        // Use base Workflow API configuration
        app.UseWorkflowApiBase();
        app.MapAppHealthChecks();

        // Add Execution-specific middleware and configurations
        ConfigureExecutionSpecificMiddleware(app);
        
        // Add Dapr scheduled job handler (Execution-specific)
        app.MapDaprScheduledJobHandler(async (string jobName, ReadOnlyMemory<byte> jobPayload, JobDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            await dispatcher.DispatchAsync(jobName, jobPayload, cancellationToken);
        });
        
        return app;
    }

    private static void ConfigureExecutionSpecificMiddleware(WebApplication app)
    {
        // Add any Execution-specific middleware here
    }
} 