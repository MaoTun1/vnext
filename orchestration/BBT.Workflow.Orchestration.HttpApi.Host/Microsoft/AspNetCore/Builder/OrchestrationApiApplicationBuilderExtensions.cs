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
        
        WorkflowApiBaseApplicationBuilderExtensions.MigrateMessagingDbContext(app.Services);
        return app;
    }
}
 