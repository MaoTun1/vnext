using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.DataSink;

/// <summary>
/// Thread-safe registry for managing data sinks
/// </summary>
public class DataSinkRegistry : IDataSinkRegistry
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, IDataSink>> _dataSinks;
    private readonly ILogger<DataSinkRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the DataSinkRegistry class
    /// </summary>
    /// <param name="logger">The logger instance</param>
    public DataSinkRegistry(ILogger<DataSinkRegistry> logger)
    {
        _dataSinks = new ConcurrentDictionary<Type, ConcurrentDictionary<string, IDataSink>>();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a data sink for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="dataSink">The data sink implementation</param>
    public void Register<TEntity>(IDataSink<TEntity> dataSink)
    {
        if (dataSink == null)
        {
            throw new ArgumentNullException(nameof(dataSink));
        }

        var entityType = typeof(TEntity);
        var sinksForType = _dataSinks.GetOrAdd(entityType, _ => new ConcurrentDictionary<string, IDataSink>());
        
        if (sinksForType.TryAdd(dataSink.Name, (IDataSink)dataSink))
        {
            _logger.LogInformation("Registered data sink {DataSinkName} for entity type {EntityType}", 
                dataSink.Name, entityType.Name);
        }
        else
        {
            _logger.LogWarning("Data sink {DataSinkName} for entity type {EntityType} is already registered", 
                dataSink.Name, entityType.Name);
        }
    }

    /// <summary>
    /// Gets all registered data sinks for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <returns>Collection of data sinks for the entity type</returns>
    public IEnumerable<IDataSink<TEntity>> GetDataSinks<TEntity>()
    {
        var entityType = typeof(TEntity);
        if (_dataSinks.TryGetValue(entityType, out var sinksForType))
        {
            return sinksForType.Values.OfType<IDataSink<TEntity>>();
        }
        
        return Enumerable.Empty<IDataSink<TEntity>>();
    }

    /// <summary>
    /// Gets all registered data sinks for a specific entity type
    /// </summary>
    /// <param name="entityType">The entity type</param>
    /// <returns>Collection of data sinks for the entity type</returns>
    public IEnumerable<IDataSink> GetDataSinks(Type entityType)
    {
        if (entityType == null)
        {
            throw new ArgumentNullException(nameof(entityType));
        }

        if (_dataSinks.TryGetValue(entityType, out var sinksForType))
        {
            return sinksForType.Values;
        }
        
        return Enumerable.Empty<IDataSink>();
    }

    /// <summary>
    /// Gets all registered data sinks
    /// </summary>
    /// <returns>Collection of all registered data sinks</returns>
    public IEnumerable<IDataSink> GetAllDataSinks()
    {
        return _dataSinks.Values.SelectMany(sinks => sinks.Values);
    }

    /// <summary>
    /// Removes a data sink for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="name">The name of the data sink to remove</param>
    public void Unregister<TEntity>(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        var entityType = typeof(TEntity);
        if (_dataSinks.TryGetValue(entityType, out var sinksForType))
        {
            if (sinksForType.TryRemove(name, out var removedSink))
            {
                _logger.LogInformation("Unregistered data sink {DataSinkName} for entity type {EntityType}", 
                    name, entityType.Name);
            }
            else
            {
                _logger.LogWarning("Data sink {DataSinkName} for entity type {EntityType} was not found", 
                    name, entityType.Name);
            }
        }
    }

    /// <summary>
    /// Clears all registered data sinks
    /// </summary>
    public void Clear()
    {
        var totalSinks = _dataSinks.Values.Sum(sinks => sinks.Count);
        _dataSinks.Clear();
        _logger.LogInformation("Cleared all data sinks. Total removed: {TotalSinks}", totalSinks);
    }
}
