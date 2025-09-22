using BBT.Workflow.Instances;

namespace BBT.Workflow.DataSink;

/// <summary>
/// Manager for handling data sink operations
/// </summary>
public interface IDataSinkManager
{
    /// <summary>
    /// Handles insert operation for an entity through all registered data sinks
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entity">The entity to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleInsertAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles update operation for an entity through all registered data sinks
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entity">The entity to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleUpdateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles delete operation for an entity through all registered data sinks
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entity">The entity to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleDeleteAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes all registered data sinks
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task FlushAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes data sinks for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task FlushAsync<TEntity>(CancellationToken cancellationToken = default);
}
