using Dapr.Jobs.Models;

namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Provides functionality for enqueueing background jobs with scheduling capabilities.
/// This service integrates with Dapr's job scheduling infrastructure to manage
/// workflow-related background tasks.
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// Enqueues a background job with the specified parameters and schedule.
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
    /// <exception cref="InvalidOperationException">Thrown when the job scheduling service is unavailable.</exception>
    Task EnqueueAsync<T>(
        string jobName,
        string jobId,
        DaprJobSchedule schedule,
        T payload,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where T : class;
}   