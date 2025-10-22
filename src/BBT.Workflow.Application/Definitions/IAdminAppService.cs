using BBT.Aether.Application;
using BBT.Aether.Domain.Entities;
using BBT.Aether.Validation;
using BBT.Workflow.Domain;
using BBT.Workflow.ExceptionHandling;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Administrative service interface for workflow management operations
/// </summary>
public interface IAdminAppService : IApplicationService
{
    /// <summary>
    /// Publishes a workflow definition to the system
    /// </summary>
    /// <param name="input">The publish input containing workflow definition, key, flow, version and optional data</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A Result representing the success or failure of the publish operation</returns>
    /// <remarks>
    /// This method validates the workflow schema, creates or updates workflow instances,
    /// handles versioning, and processes additional data if provided.
    /// Returns Result.Fail with appropriate error code when schema is invalid or version already exists.
    /// </remarks>
    /// <exception cref="AetherValidationException">Thrown when workflow validation fails</exception>
    Task<Result> PublishAsync(PublishInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache for a specific workflow instance
    /// </summary>
    /// <param name="input">The invalidate cache input containing flow, key, version and domain information</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A Result representing the success or failure of the cache invalidation operation</returns>
    /// <remarks>
    /// This method finds the specified workflow instance and processes it through the cast processor
    /// to invalidate and refresh the cache.
    /// Returns Result.Fail with appropriate error code when schema is invalid or entity is not found.
    /// </remarks>
    /// <exception cref="EntityNotFoundException">Thrown when the instance or instance data is not found</exception>
    Task<Result> InvalidateCacheAsync(InvalidateCacheInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-initializes the workflow system by reloading all system components
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous re-initialization operation</returns>
    /// <remarks>
    /// This method reloads all system components including workflows, tasks, functions, 
    /// views, schemas, and extensions from the runtime system schema and reinitializes the domain cache.
    /// </remarks>
    Task ReInitializeAsync(CancellationToken cancellationToken = default);
}