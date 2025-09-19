namespace BBT.Workflow.DataSink;

/// <summary>
/// Represents the type of data sink operation
/// </summary>
public enum DataSinkOperation
{
    /// <summary>
    /// Insert operation
    /// </summary>
    Insert,

    /// <summary>
    /// Update operation
    /// </summary>
    Update,

    /// <summary>
    /// Delete operation
    /// </summary>
    Delete
}

/// <summary>
/// Represents a data sink operation with entity and operation type
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
public class DataSinkOperation<TEntity>
{
    /// <summary>
    /// Gets the entity
    /// </summary>
    public TEntity Entity { get; }

    /// <summary>
    /// Gets the operation type
    /// </summary>
    public DataSinkOperation Operation { get; }

    /// <summary>
    /// Gets the timestamp when the operation occurred
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the DataSinkOperation class
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <param name="operation">The operation type</param>
    public DataSinkOperation(TEntity entity, DataSinkOperation operation)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        Operation = operation;
        Timestamp = DateTime.UtcNow;
    }
}
