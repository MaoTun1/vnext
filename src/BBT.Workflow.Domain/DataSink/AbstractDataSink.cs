using Microsoft.Extensions.Logging;

namespace BBT.Workflow.DataSink;

/// <summary>
/// Abstract base class for data sink implementations
/// </summary>
/// <typeparam name="TEntity">The entity type this sink handles</typeparam>
public abstract class AbstractDataSink<TEntity> : IDataSink<TEntity>
{
    /// <summary>
    /// Gets the logger instance
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Initializes a new instance of the AbstractDataSink class
    /// </summary>
    /// <param name="logger">The logger instance</param>
    protected AbstractDataSink(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the name of this data sink
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets whether this data sink is enabled
    /// </summary>
    public abstract bool IsEnabled { get; }

    /// <summary>
    /// Gets the entity type this sink handles
    /// </summary>
    public Type EntityType => typeof(TEntity);

    /// <summary>
    /// Handles data transfer for insert operations
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public virtual async Task HandleInsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            await OnInsertAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle insert operation in data sink {DataSinkName} for entity {EntityType}", 
                Name, typeof(TEntity).Name);
            // Don't rethrow to avoid affecting the main operation
        }
    }

    /// <summary>
    /// Handles data transfer for update operations
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public virtual async Task HandleUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            await OnUpdateAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle update operation in data sink {DataSinkName} for entity {EntityType}", 
                Name, typeof(TEntity).Name);
            // Don't rethrow to avoid affecting the main operation
        }
    }

    /// <summary>
    /// Handles data transfer for delete operations
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public virtual async Task HandleDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            await OnDeleteAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle delete operation in data sink {DataSinkName} for entity {EntityType}", 
                Name, typeof(TEntity).Name);
            // Don't rethrow to avoid affecting the main operation
        }
    }

    /// <summary>
    /// Flushes any pending data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public virtual async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            await OnFlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to flush data in data sink {DataSinkName}", Name);
            // Don't rethrow to avoid affecting the main operation
        }
    }

    /// <summary>
    /// Called when an insert operation occurs
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected abstract Task OnInsertAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Called when an update operation occurs
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected abstract Task OnUpdateAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Called when a delete operation occurs
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected abstract Task OnDeleteAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Called when flushing pending data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected abstract Task OnFlushAsync(CancellationToken cancellationToken);
}
