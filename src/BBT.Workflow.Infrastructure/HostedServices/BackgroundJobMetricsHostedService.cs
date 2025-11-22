using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.HostedServices;

/// <summary>
/// Hosted service that periodically updates background job metrics by monitoring the job store.
/// This service tracks pending and running job counts for comprehensive background job monitoring.
/// </summary>
public sealed class BackgroundJobMetricsHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BackgroundJobMetricsHostedService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(1); // Update metrics every minute

    /// <summary>
    /// Initializes a new instance of the BackgroundJobMetricsHostedService.
    /// </summary>
    /// <param name="serviceScopeFactory">Service scope factory for creating scoped dependencies</param>
    /// <param name="logger">Logger for recording service activities</param>
    public BackgroundJobMetricsHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BackgroundJobMetricsHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the background job metrics collection loop.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Job Metrics Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateJobMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating background job metrics");
            }

            try
            {
                await Task.Delay(_updateInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
        }

        _logger.LogInformation("Background Job Metrics Service stopped");
    }

    /// <summary>
    /// Updates background job metrics by querying the job store for current job counts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task UpdateJobMetricsAsync(CancellationToken cancellationToken)
    {
        // using var scope = _serviceScopeFactory.CreateScope();
        // var workflowMetrics = scope.ServiceProvider.GetRequiredService<IWorkflowMetrics>();
        // var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
        //
        // try
        // {
        //     _logger.LogDebug("Updating background job metrics");
        //
        //     // Get pending jobs count - jobs that are scheduled but not triggered
        //     var pendingJobs = await jobStore.GetListByActiveAsync<object>(cancellationToken);
        //     var jobGroups = pendingJobs.GroupBy(job => job.JobName);
        //
        //     foreach (var jobGroup in jobGroups)
        //     {
        //         var jobName = jobGroup.Key;
        //         var pendingCount = jobGroup.Count();
        //         
        //         // Set pending jobs count by job type
        //         workflowMetrics.SetBackgroundJobsPending(jobName, pendingCount);
        //         
        //         _logger.LogDebug("Updated metrics for job type '{JobName}': {PendingCount} pending jobs", 
        //             jobName, pendingCount);
        //     }
        //
        //     // Note: Running jobs count is managed real-time in JobDispatcher
        //     // We only update pending jobs here as they can be queried from the job store
        //
        //     _logger.LogDebug("Background job metrics updated successfully");
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Failed to update background job metrics");
        //     throw;
        // }
    }
}