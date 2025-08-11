namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Defines the contract for persisting and retrieving background job information.
/// This interface provides data access operations for managing job state,
/// scheduling information, and execution tracking within the workflow system.
/// </summary>
public interface IJobStore
{
    /// <summary>
    /// Saves or updates background job information in the persistent store.
    /// If a job with the same ID already exists, it will be updated; otherwise, a new record is created.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="jobId">The unique identifier for the job instance.</param>
    /// <param name="job">The complete job information including payload, metadata, and scheduling details.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when jobId or job is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the job cannot be saved due to storage issues.</exception>
    Task SaveAsync<T>(string jobId, BackgroundJobInfo<T> job, CancellationToken cancellationToken = default) where T: class;
    
    /// <summary>
    /// Retrieves background job information for the specified job ID.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="jobId">The unique identifier of the job to retrieve.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous retrieval operation.
    /// The result contains the job information if found; otherwise, null.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when jobId is null or empty.</exception>
    Task<BackgroundJobInfo<T>?> GetAsync<T>(string jobId, CancellationToken cancellationToken = default) where T: class;
    
    /// <summary>
    /// Retrieves a collection of all active (non-triggered) background jobs.
    /// This method is typically used for job recovery scenarios or monitoring purposes.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous retrieval operation.
    /// The result contains a collection of active job information.
    /// </returns>
    /// <remarks>
    /// Active jobs are those that have been scheduled but not yet triggered for execution.
    /// This method excludes jobs that have already been processed or completed.
    /// </remarks>
    Task<IEnumerable<BackgroundJobInfo<T>>> GetListByActiveAsync<T>(CancellationToken cancellationToken = default) where T: class;
}