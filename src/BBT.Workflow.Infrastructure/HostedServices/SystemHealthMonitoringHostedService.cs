using BBT.Workflow.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.HostedServices;

/// <summary>
/// Hosted service that periodically monitors system health and updates health status metrics.
/// This service tracks error rates and overall system health status for monitoring and alerting.
/// </summary>
public sealed class SystemHealthMonitoringHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SystemHealthMonitoringHostedService> _logger;
    private readonly TimeSpan _monitoringInterval = TimeSpan.FromMinutes(5); // Monitor health every 5 minutes
    
    private readonly Dictionary<string, ErrorRateTracker> _errorRateTrackers = new();
    
    /// <summary>
    /// Initializes a new instance of the SystemHealthMonitoringHostedService.
    /// </summary>
    /// <param name="serviceScopeFactory">Service scope factory for creating scoped dependencies</param>
    /// <param name="logger">Logger for recording service activities</param>
    public SystemHealthMonitoringHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SystemHealthMonitoringHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        
        // Initialize error rate trackers for different components
        _errorRateTrackers["ExceptionHandler"] = new ErrorRateTracker();
        _errorRateTrackers["HttpMiddleware"] = new ErrorRateTracker();
        _errorRateTrackers["StateMachineExecutor"] = new ErrorRateTracker();
        _errorRateTrackers["ScriptEngine"] = new ErrorRateTracker();
        _errorRateTrackers["DatabaseInterceptor"] = new ErrorRateTracker();
        _errorRateTrackers["BackgroundJobs"] = new ErrorRateTracker();
        _errorRateTrackers["Overall"] = new ErrorRateTracker();
    }

    /// <summary>
    /// Executes the system health monitoring loop.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("System Health Monitoring Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateSystemHealthMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating system health metrics");
            }

            try
            {
                await Task.Delay(_monitoringInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
        }

        _logger.LogInformation("System Health Monitoring Service stopped");
    }

    /// <summary>
    /// Updates system health metrics by calculating error rates and health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private Task UpdateSystemHealthMetricsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var workflowMetrics = scope.ServiceProvider.GetRequiredService<IWorkflowMetrics>();

        try
        {
            // Calculate and update error rates for each component
            foreach (var (component, tracker) in _errorRateTrackers)
            {
                var errorRate = CalculateErrorRate(tracker);
                workflowMetrics.SetWorkflowErrorRate(component, errorRate);
                
                // Determine health status based on error rate thresholds
                var isHealthy = DetermineHealthStatus(errorRate);
                workflowMetrics.SetWorkflowHealthStatus(component, isHealthy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update system health metrics");
            
            // Set overall system as unhealthy if monitoring fails
            workflowMetrics.SetWorkflowHealthStatus("Overall", false);
            throw;
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Calculates error rate based on recent error tracking.
    /// </summary>
    /// <param name="tracker">Error rate tracker for the component</param>
    /// <returns>Error rate as percentage (0.0 to 100.0)</returns>
    private static double CalculateErrorRate(ErrorRateTracker tracker)
    {
        // Simple error rate calculation based on recent activity
        // In a real implementation, this would track actual success/error counts
        var recentErrorCount = tracker.RecentErrorCount;
        var totalRequests = Math.Max(tracker.TotalRequests, 1); // Avoid division by zero
        
        return Math.Min((double)recentErrorCount / totalRequests * 100.0, 100.0);
    }

    /// <summary>
    /// Determines health status based on error rate thresholds.
    /// </summary>
    /// <param name="errorRate">Current error rate percentage</param>
    /// <returns>True if healthy, false if unhealthy</returns>
    private static bool DetermineHealthStatus(double errorRate)
    {
        // Define health thresholds
        const double unhealthyThreshold = 10.0; // 10% error rate threshold
        
        return errorRate < unhealthyThreshold;
    }
}

/// <summary>
/// Simple error rate tracker for monitoring component health.
/// In a production system, this would be more sophisticated and persistent.
/// </summary>
public class ErrorRateTracker
{
    public int RecentErrorCount { get; set; } = 0;
    public int TotalRequests { get; set; } = 100; // Default baseline
    
    public void RecordError()
    {
        RecentErrorCount++;
        TotalRequests++;
    }
    
    public void RecordSuccess()
    {
        TotalRequests++;
    }
    
    public void Reset()
    {
        RecentErrorCount = 0;
        TotalRequests = 100;
    }
}