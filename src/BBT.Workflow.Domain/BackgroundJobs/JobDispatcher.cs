using BBT.Workflow.Monitoring;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Provides centralized job dispatching functionality by routing background jobs to their appropriate handlers.
/// This class acts as a mediator between the job scheduling system and the specific job handlers,
/// ensuring that each job is processed by the correct handler based on the job name.
/// </summary>
/// <param name="handlers">Collection of available job handlers that can process different types of jobs.</param>
/// <param name="workflowMetrics">Service for recording background job metrics.</param>
/// <param name="logger">Logger instance for recording job dispatching activities and errors.</param>
public sealed class JobDispatcher(
    IEnumerable<IJobHandler> handlers,
    IWorkflowMetrics workflowMetrics,
    ILogger<JobDispatcher> logger)
{
    /// <summary>
    /// Dispatches a background job to the appropriate handler based on the job name.
    /// If no matching handler is found, the operation is logged and gracefully ignored.
    /// </summary>
    /// <param name="jobName">The name of the job that determines which handler should process it.</param>
    /// <param name="jobPayload">The serialized job payload data to be processed by the handler.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during job processing.</param>
    /// <returns>A task representing the asynchronous job dispatching and processing operation.</returns>
    /// <remarks>
    /// This method performs a linear search through the available handlers to find a match.
    /// If multiple handlers exist for the same job name, only the first one found will be used.
    /// </remarks>
    public async Task DispatchAsync(string jobName, ReadOnlyMemory<byte> jobPayload,
        CancellationToken cancellationToken)
    {
        var handler = handlers.FirstOrDefault(h => h.JobName == jobName);
        if (handler == null)
        {
            logger.LogWarning("No handler found for job '{JobName}'", jobName);
            // Record failed job execution due to missing handler
            workflowMetrics.RecordBackgroundJobFailed("Unknown", jobName, "NoHandlerFound");
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var handlerType = handler.GetType().Name;
        
        // Record job execution start
        workflowMetrics.SetBackgroundJobsRunning(handlerType, 1); // Increment running count

        try
        {
            logger.LogInformation("Dispatching job '{JobName}' to handler '{HandlerType}'", jobName, handlerType);
            
            await handler.HandleAsync(jobPayload, cancellationToken);
            
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record successful job execution
            workflowMetrics.RecordBackgroundJobExecuted(handlerType, jobName, "success");
            workflowMetrics.RecordBackgroundJobDuration(handlerType, jobName, "success", durationSeconds);
            
            logger.LogInformation("Successfully completed job '{JobName}' with handler '{HandlerType}' in {Duration:F3}s",
                jobName, handlerType, durationSeconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record cancelled job execution
            workflowMetrics.RecordBackgroundJobExecuted(handlerType, jobName, "cancelled");
            workflowMetrics.RecordBackgroundJobDuration(handlerType, jobName, "cancelled", durationSeconds);
            
            logger.LogWarning("Job '{JobName}' was cancelled after {Duration:F3}s", jobName, durationSeconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record failed job execution
            workflowMetrics.RecordBackgroundJobExecuted(handlerType, jobName, "failed");
            workflowMetrics.RecordBackgroundJobFailed(handlerType, jobName, ex.GetType().Name);
            workflowMetrics.RecordBackgroundJobDuration(handlerType, jobName, "failed", durationSeconds);
            
            logger.LogError(ex, "Job '{JobName}' failed after {Duration:F3}s", jobName, durationSeconds);
            throw;
        }
        finally
        {
            // Record job execution end
            workflowMetrics.SetBackgroundJobsRunning(handlerType, 0); // Decrement running count
        }
    }
}