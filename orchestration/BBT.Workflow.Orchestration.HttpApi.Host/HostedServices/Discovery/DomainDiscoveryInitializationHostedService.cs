using BBT.Workflow.Discovery;
using BBT.Workflow.ExceptionHandling;

namespace BBT.Workflow.HostedServices;

/// <summary>
/// Background service that handles domain registration and bulk cache initialization at startup.
/// Registers the current domain with service discovery, then fetches and caches all active domains.
/// </summary>
public sealed class DomainDiscoveryInitializationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DomainDiscoveryInitializationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting domain discovery initialization...");
            
            await using var scope = scopeFactory.CreateAsyncScope();
            var registrationService = scope.ServiceProvider.GetRequiredService<IDomainRegistrationService>();
            var discoveryResolver = scope.ServiceProvider.GetRequiredService<IDomainDiscoveryResolver>();
            
            // Step 1: Register this domain
            await registrationService.RegisterDomainAsync(stoppingToken);
            
            // Step 2: Fetch and cache all domains
            await discoveryResolver.RefreshBulkCacheAsync(stoppingToken);
            
            logger.LogInformation("Domain discovery initialization completed successfully");
        }
        catch (DomainRegistrationFailedException ex)
        {
            logger.LogCritical(ex, "Domain registration failed. Application startup will be aborted.");
            throw;
        }
        catch (InvalidConfigurationException ex)
        {
            logger.LogCritical(ex, "Invalid discovery configuration. Application startup will be aborted.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during domain discovery initialization. Application will continue without discovery cache.");
        }
    }
}
