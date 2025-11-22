namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Application builder extensions specific to Worker Inbox
/// </summary>
public static class InboxWorkerApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the application pipeline with base worker inbox middleware
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication UseWorkerInbox(this WebApplication app)
    {
        app.UseWorkflowApiBase();
        app.MapAppHealthChecks();
        return app;
    }
}