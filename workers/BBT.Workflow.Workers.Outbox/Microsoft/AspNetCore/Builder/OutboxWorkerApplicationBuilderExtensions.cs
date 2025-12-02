namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Application builder extensions specific to Worker Outbox
/// </summary>
public static class OutboxWorkerApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the application pipeline with base worker outbox middleware
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication UseWorkerOutbox(this WebApplication app)
    {
        app.UseWorkflowApiBase();
        app.MapAppHealthChecks();
        return app;
    }
}