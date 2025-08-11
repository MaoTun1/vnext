using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;

namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Provides an Entity Framework Core implementation of the IJobStore interface for persisting
/// and retrieving background job information. This implementation uses the workflow's
/// multi-schema support and integrates with the instance job repository infrastructure.
/// </summary>
/// <param name="currentSchema">Service for managing the current schema context for multi-tenant operations.</param>
/// <param name="jobRepository">Repository for accessing and manipulating instance job data.</param>
/// <param name="guidGenerator">Service for generating unique identifiers for new job records.</param>
public sealed class EfCoreJobStore(
    ICurrentSchema currentSchema,
    IInstanceJobRepository jobRepository,
    IGuidGenerator guidGenerator
) : IJobStore
{
    /// <summary>
    /// Saves or updates background job information in the database using Entity Framework Core.
    /// The operation is performed within the appropriate schema context based on the job's domain.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="jobId">The unique identifier for the job instance.</param>
    /// <param name="job">The complete job information including payload, metadata, and scheduling details.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    /// <remarks>
    /// This method extracts the domain from the job metadata to determine the correct schema context.
    /// If the job already exists, it updates the trigger status and expression value.
    /// If the job doesn't exist, it creates a new InstanceJob entity with serialized payload data.
    /// The operation is performed within a schema context switch to ensure proper multi-tenant isolation.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when jobId or job is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the domain cannot be determined or database operation fails.</exception>
    public async Task SaveAsync<T>(string jobId, BackgroundJobInfo<T> job,
        CancellationToken cancellationToken = default) where T : class
    {
        var domain = job.GetDomain();
        if (domain.IsNullOrEmpty())
        {
            return;
        }

        using (currentSchema.Change(domain))
        {
            var instanceJob = await jobRepository.FindByNameAsync(jobId, cancellationToken);
            if (instanceJob != null)
            {
                if (job.IsTriggered)
                {
                    instanceJob.Triggered();
                }

                instanceJob.UpdateTriggerAt(job.ExpressionValue);
                await jobRepository.UpdateAsync(instanceJob, true, cancellationToken);
            }
            else
            {
                instanceJob = InstanceJob.Create(
                    guidGenerator.Create(),
                    job.JobName,
                    job.JobId,
                    job.GetDomain()!,
                    job.GetFlowName()!,
                    job.GetInstanceId(),
                    job.ExpressionValue,
                    JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(job.Payload))
                );
                await jobRepository.InsertAsync(instanceJob, true, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Retrieves background job information for the specified job ID from the database.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="jobId">The unique identifier of the job to retrieve.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous retrieval operation.
    /// The result contains the job information if found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// This method retrieves the InstanceJob entity by job ID and maps it to a BackgroundJobInfo instance.
    /// The job payload is deserialized from the stored JsonElement to the specified type T.
    /// Metadata is reconstructed from the stored domain, flow name, and instance ID values.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when jobId is null or empty.</exception>
    public async Task<BackgroundJobInfo<T>?> GetAsync<T>(string jobId, CancellationToken cancellationToken = default)
        where T : class
    {
        var instanceJob = await jobRepository.FindByNameAsync(jobId, cancellationToken);
        if (instanceJob == null)
            return null;

        return Map<T>(instanceJob);
    }

    /// <summary>
    /// Retrieves a collection of all active (non-triggered) background jobs from the database.
    /// Active jobs are those that have been scheduled but not yet executed.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous retrieval operation.
    /// The result contains a collection of active job information.
    /// </returns>
    /// <remarks>
    /// This method queries for all untriggered jobs using the repository and maps each result
    /// to a BackgroundJobInfo instance. This is typically used for job recovery scenarios
    /// or monitoring purposes to identify pending jobs.
    /// </remarks>
    public async Task<IEnumerable<BackgroundJobInfo<T>>> GetListByActiveAsync<T>(
        CancellationToken cancellationToken = default)
        where T : class
    {
        return (await jobRepository.GetListUntriggeredAsync(cancellationToken))
            .Select(Map<T>);
    }

    /// <summary>
    /// Maps an InstanceJob entity to a BackgroundJobInfo instance with proper type conversion and metadata reconstruction.
    /// </summary>
    /// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
    /// <param name="instanceJob">The InstanceJob entity to map from.</param>
    /// <returns>A BackgroundJobInfo instance containing the mapped data.</returns>
    /// <remarks>
    /// This private method handles the conversion between the database entity representation
    /// and the domain model. It deserializes the JSON payload and reconstructs the metadata
    /// dictionary with domain, flow name, and instance ID information.
    /// </remarks>
    private static BackgroundJobInfo<T> Map<T>(InstanceJob instanceJob) where T : class
    {
        return new BackgroundJobInfo<T>
        {
            JobName = instanceJob.JobName,
            JobId = instanceJob.JobId,
            ExpressionValue = instanceJob.ExpressionValue,
            Payload = instanceJob.Payload.JsonElement.Deserialize<T>()!,
            Metadata = new Dictionary<string, string>()
            {
                { "domain", instanceJob.Domain },
                { "flowName", instanceJob.FlowName },
                { "instanceId", instanceJob.InstanceId.ToString() }
            }
        };
    }
}