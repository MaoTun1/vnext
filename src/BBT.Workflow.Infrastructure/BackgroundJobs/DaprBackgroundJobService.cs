using System.Text.Json;
using BBT.Workflow.Monitoring;
using Dapr.Client;
using Dapr.Jobs;
using Dapr.Jobs.Models;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Provides an implementation of IBackgroundJobService that integrates with Dapr's job scheduling capabilities.
/// This service handles the scheduling of background jobs using Dapr's distributed job scheduling infrastructure
/// while also persisting job information for tracking and recovery purposes.
/// </summary>
/// <param name="logger">Logger instance for recording job scheduling activities and errors.</param>
/// <param name="daprJobsClient">Dapr jobs client for interacting with the Dapr job scheduling service.</param>
/// <param name="jobStore">Job store for persisting job information and state.</param>
/// <param name="workflowMetrics">Service for recording background job metrics.</param>
public sealed class DaprBackgroundJobService(
    ILogger<DaprBackgroundJobService> logger,
    DaprJobsClient daprJobsClient,
    IJobStore jobStore,
    IWorkflowMetrics workflowMetrics
) : IBackgroundJobService
{
    /// <summary>
    /// Enqueues a background job with the specified parameters and schedule using Dapr's job scheduling service.
    /// The job information is first persisted to the job store, then scheduled with Dapr for execution.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="jobName">The unique name identifying the type of job to be executed.</param>
    /// <param name="jobId">A unique identifier for this specific job instance.</param>
    /// <param name="schedule">The Dapr job schedule defining when the job should be executed.</param>
    /// <param name="payload">The data payload to be passed to the job handler when executed.</param>
    /// <param name="metadata">Common metadata includes domain, flow name, and instance ID information.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous enqueue operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Dapr job service is unavailable or scheduling fails.</exception>
    /// <remarks>
    /// This method creates a BackgroundJobInfo instance containing all job details and metadata,
    /// saves it to the job store for persistence, then schedules the job with Dapr.
    /// The scheduled job will contain the serialized job information as its payload.
    /// </remarks>
    public async Task EnqueueAsync<T>(
        string jobName,
        string jobId,
        DaprJobSchedule schedule,
        T payload,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where T : class
    {
        logger.LogInformation("Scheduling job {jobName} - {jobId}.", jobName, jobId);

        var jobData = new BackgroundJobInfo<T>
        {
            JobName = jobName,
            JobId = jobId,
            ExpressionValue = schedule.ExpressionValue,
            Payload = payload,
            IsTriggered = false,
            Metadata = metadata
        };

        try
        {
            await jobStore.SaveAsync(jobId, jobData, cancellationToken);
#pragma warning disable CS0618 // Type or member is obsolete
            await daprJobsClient.ScheduleJobAsync(
                jobName,
                schedule,
                JsonSerializer.SerializeToUtf8Bytes(jobData),
                cancellationToken: cancellationToken
            );
#pragma warning restore CS0618 // Type or member is obsolete

            // Record successful background job scheduling
            workflowMetrics.RecordBackgroundJobScheduled(typeof(T).Name, jobName);
            
            logger.LogInformation("Successfully scheduled job {jobName} - {jobId}.", jobName, jobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule job {jobName} - {jobId}.", jobName, jobId);
            throw;
        }
    }
}