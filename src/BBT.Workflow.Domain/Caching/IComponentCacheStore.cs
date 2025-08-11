using BBT.Aether.Domain.Entities;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Caching;

/// <summary>
/// Provides caching operations for workflow components including flows, tasks, schemas, functions, views, and extensions.
/// Supports domain-specific caching with versioning capabilities.
/// </summary>
public interface IComponentCacheStore
{
    /// <summary>
    /// Retrieves a workflow definition from the cache.
    /// </summary>
    /// <param name="domain">The domain identifier where the workflow belongs.</param>
    /// <param name="key">The unique key/name identifier of the workflow.</param>
    /// <param name="version">The specific version of the workflow. If null, returns the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The task result contains the <see cref="Definitions.Workflow"/> if found.
    /// </returns>
    /// <exception cref="EntityNotFoundException">Thrown when the workflow is not found in cache.</exception>
    Task<Definitions.Workflow> GetFlowAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a workflow task definition from the cache.
    /// </summary>
    /// <param name="domain">The domain identifier where the task belongs.</param>
    /// <param name="key">The unique key/name identifier of the task.</param>
    /// <param name="version">The specific version of the task. If null, returns the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The task result contains the <see cref="WorkflowTask"/> if found.
    /// </returns>
    /// <exception cref="EntityNotFoundException">Thrown when the task is not found in cache.</exception>
    Task<WorkflowTask> GetTaskAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a schema definition from the cache.
    /// </summary>
    /// <param name="domain">The domain identifier where the schema belongs.</param>
    /// <param name="key">The unique key/name identifier of the schema.</param>
    /// <param name="version">The specific version of the schema. If null, returns the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The task result contains the <see cref="SchemaDefinition"/> if found.
    /// </returns>
    /// <exception cref="EntityNotFoundException">Thrown when the schema is not found in cache.</exception>
    Task<SchemaDefinition> GetSchemaAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a function definition from the cache.
    /// </summary>
    /// <param name="domain">The domain identifier where the function belongs.</param>
    /// <param name="key">The unique key/name identifier of the function.</param>
    /// <param name="version">The specific version of the function. If null, returns the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The task result contains the <see cref="Function"/> if found.
    /// </returns>
    /// <exception cref="EntityNotFoundException">Thrown when the function is not found in cache.</exception>
    Task<Function> GetFunctionAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a view definition from the cache.
    /// </summary>
    /// <param name="domain">The domain identifier where the view belongs.</param>
    /// <param name="key">The unique key/name identifier of the view.</param>
    /// <param name="version">The specific version of the view. If null, returns the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The task result contains the <see cref="View"/> if found.
    /// </returns>
    /// <exception cref="EntityNotFoundException">Thrown when the view is not found in cache.</exception>
    Task<View> GetViewAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an extension definition from the cache.
    /// </summary>
    /// <param name="domain">The domain identifier where the extension belongs.</param>
    /// <param name="key">The unique key/name identifier of the extension.</param>
    /// <param name="version">The specific version of the extension. If null, returns the latest version.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The task result contains the <see cref="Extension"/> if found.
    /// </returns>
    /// <exception cref="EntityNotFoundException">Thrown when the extension is not found in cache.</exception>
    Task<Extension> GetExtensionAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all extension definitions for the specified domain from the cache.
    /// This method is used to get core extensions that should be executed runtime-wide.
    /// </summary>
    /// <param name="domain">The domain identifier to retrieve extensions for.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The task result contains a collection of all <see cref="Extension"/> definitions for the domain.
    /// </returns>
    Task<IEnumerable<Extension>> GetAllExtensionsAsync(
        string domain,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an entity in the cache.
    /// </summary>
    /// <typeparam name="T">The type of entity to store. Must implement <see cref="IDomainEntity"/>.</typeparam>
    /// <param name="entity">The entity instance to be cached.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous cache operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the entity type is not supported for caching.</exception>
    public Task SetAsync<T>(T entity, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter;
}