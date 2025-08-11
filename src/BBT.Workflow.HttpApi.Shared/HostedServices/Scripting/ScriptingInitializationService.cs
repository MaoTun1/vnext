using BBT.Workflow.Scripting.Functions;
using Dapr.Client;
using Microsoft.Extensions.Hosting;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Service that initializes scripting infrastructure during application startup.
/// This service configures the ScriptHelper with the DaprClient instance for
/// enabling distributed application runtime capabilities in workflow scripts.
/// </summary>
/// <param name="daprClient">The Dapr client instance for distributed application runtime operations</param>
/// <exception cref="ArgumentNullException">Thrown when daprClient is null</exception>
public sealed class ScriptingInitializationService(DaprClient daprClient) : IHostedService
{
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Initialize ScriptHelper with DaprClient
        ScriptHelper.SetDaprClient(_daprClient);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}