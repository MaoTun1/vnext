namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Defines the contract for background job handlers that process specific types of scheduled jobs.
/// Implementations of this interface are responsible for handling the execution logic
/// for different types of background jobs within the workflow system.
/// </summary>
public interface IJobHandler
{
    /// <summary>
    /// Gets the unique name of the job type that this handler can process.
    /// This name is used by the job dispatcher to route jobs to the appropriate handler.
    /// </summary>
    /// <value>A string representing the job name that this handler supports.</value>
    string JobName { get; }
    
    /// <summary>
    /// Handles the execution of a background job with the specified payload data.
    /// This method contains the core business logic for processing the job.
    /// </summary>
    /// <param name="jobPayload">The serialized job payload data containing all necessary information for job execution.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during job processing.</param>
    /// <returns>A task representing the asynchronous job processing operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the job payload cannot be deserialized or is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the job cannot be processed due to system state issues.</exception>
    Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken);
}