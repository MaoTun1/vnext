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
/// <exception cref="ArgumentNullException">Thrown when daprClient is null</exception>
public sealed class ScriptingInitializationService(
    DaprClient daprClient,
    ILogger<ScriptContext> logger,
    IConfiguration configuration) : IHostedService
{
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly ILogger<ScriptContext> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Initialize ScriptHelper with DaprClient
        ScriptHelper.SetDaprClient(_daprClient);
        ScriptHelper.SetLogger(_logger);
        ScriptHelper.SetConfiguration(_configuration);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}