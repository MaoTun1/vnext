namespace BBT.Workflow.DataSink;

/// <summary>
/// Registry for managing data sinks
/// </summary>
public interface IDataSinkRegistry
{
    /// <summary>
    /// Registers a data sink for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="dataSink">The data sink implementation</param>
    void Register<TEntity>(IDataSink<TEntity> dataSink);

    /// <summary>
    /// Gets all registered data sinks for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <returns>Collection of data sinks for the entity type</returns>
    IEnumerable<IDataSink<TEntity>> GetDataSinks<TEntity>();

    /// <summary>
    /// Gets all registered data sinks for a specific entity type
    /// </summary>
    /// <param name="entityType">The entity type</param>
    /// <returns>Collection of data sinks for the entity type</returns>
    IEnumerable<IDataSink> GetDataSinks(Type entityType);

    /// <summary>
    /// Gets all registered data sinks
    /// </summary>
    /// <returns>Collection of all registered data sinks</returns>
    IEnumerable<IDataSink> GetAllDataSinks();

    /// <summary>
    /// Removes a data sink for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="name">The name of the data sink to remove</param>
    void Unregister<TEntity>(string name);

    /// <summary>
    /// Clears all registered data sinks
    /// </summary>
    void Clear();
}
