using BBT.Workflow.Caching;
using BBT.Workflow.Tasks.Execution;
using BBT.Workflow.Tasks;
using BBT.Workflow.Scripting;
using BBT.Workflow.Runtime;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions specific to Execution API
/// </summary>
public static class ExecutionApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds Execution API specific services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddExecutionApiModule(this IServiceCollection services)
    {
        // Add Execution-specific services
        services
            .AddWorkflowApiBase()
            .AddWorkflowDaprClients()
            .AddWorkflowHttpClient()
            .AddAppHealthChecks();

        // Add Execution-specific configurations
        ConfigureExecutionSpecificServices(services);
        
        return services;
    }

    private static void ConfigureExecutionSpecificServices(IServiceCollection services)
    {
        // Bind RuntimeOptions from appsettings.json "Runtime" section
        // This will override domain-level defaults with execution-specific configuration
        services.AddOptions<RuntimeOptions>()
            .BindConfiguration(RuntimeOptions.SectionName)
            .ValidateOnStart();

        // Replace default NullTaskExecutor with LocalTaskExecutor for direct task execution
        // LocalTaskExecutor executes tasks directly within this service without remote calls
        services.AddScoped<ITaskOrchestrator, LocalTaskExecutor>();
        
        // Add any Execution-specific hosted services
        services.AddHostedService<CacheInitializationHostedService>();
        services.AddHostedService<ScriptingInitializationService>();
    }
}