using BBT.Workflow.Scripting.Functions;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Service that initializes scripting infrastructure during application startup.
/// This service configures the ScriptHelper with the DaprClient instance for
/// enabling distributed application runtime capabilities in workflow scripts.
/// </summary>
/// <param name="daprClient">The Dapr client instance for distributed application runtime operations</param>
/// <param name="logger">The logger instance for logging script execution</param>
/// <param name="configuration">The configuration instance for accessing application settings</param>
public sealed class ScriptingInitializationService(
    DaprClient daprClient,
    ILogger<ScriptContext> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize ScriptHelper with DaprClient
        ScriptHelper.SetDaprClient(daprClient);
        ScriptHelper.SetLogger(logger);
        ScriptHelper.SetConfiguration(configuration);
        
        return Task.CompletedTask;
    }
}