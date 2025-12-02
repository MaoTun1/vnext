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

        return app;
    }
} 