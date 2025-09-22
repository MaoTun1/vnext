using BBT.Workflow.Instances;

namespace BBT.Workflow.DataSink;

/// <summary>
/// Interface for data sink operations that can handle analytics data transfer
/// </summary>
/// <typeparam name="TEntity">The entity type this sink handles</typeparam>
public interface IDataSink<in TEntity>
{
    /// <summary>
    /// Gets the name of this data sink
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this data sink is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Handles data transfer for insert operations
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleInsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles data transfer for update operations
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleUpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles data transfer for delete operations
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleDeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for all data sinks
/// </summary>
public interface IDataSink
{
    /// <summary>
    /// Gets the name of this data sink
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this data sink is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the entity type this sink handles
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Flushes any pending data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
