using Microsoft.Extensions.Logging;

namespace BBT.Workflow.DataSink;

/// <summary>
/// Manager for handling data sink operations
/// </summary>
public class DataSinkManager : IDataSinkManager
{
    private readonly IDataSinkRegistry _registry;
    private readonly ILogger<DataSinkManager> _logger;

    /// <summary>
    /// Initializes a new instance of the DataSinkManager class
    /// </summary>
    /// <param name="registry">The data sink registry</param>
    /// <param name="logger">The logger instance</param>
    public DataSinkManager(IDataSinkRegistry registry, ILogger<DataSinkManager> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles insert operation for an entity through all registered data sinks
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entity">The entity to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task HandleInsertAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var dataSinks = _registry.GetDataSinks<TEntity>().ToList();
        
        if (!dataSinks.Any())
        {
            _logger.LogDebug("No data sinks registered for entity type {EntityType}", typeof(TEntity).Name);
            return;
        }

        _logger.LogDebug("Processing insert operation for entity type {EntityType} through {SinkCount} data sinks", 
            typeof(TEntity).Name, dataSinks.Count);

        var tasks = dataSinks.Select(sink => sink.HandleInsertAsync(entity, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Handles update operation for an entity through all registered data sinks
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entity">The entity to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task HandleUpdateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var dataSinks = _registry.GetDataSinks<TEntity>().ToList();
        
        if (!dataSinks.Any())
        {
            _logger.LogDebug("No data sinks registered for entity type {EntityType}", typeof(TEntity).Name);
            return;
        }

        _logger.LogDebug("Processing update operation for entity type {EntityType} through {SinkCount} data sinks", 
            typeof(TEntity).Name, dataSinks.Count);

        var tasks = dataSinks.Select(sink => sink.HandleUpdateAsync(entity, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Handles delete operation for an entity through all registered data sinks
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entity">The entity to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task HandleDeleteAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var dataSinks = _registry.GetDataSinks<TEntity>().ToList();
        
        if (!dataSinks.Any())
        {
            _logger.LogDebug("No data sinks registered for entity type {EntityType}", typeof(TEntity).Name);
            return;
        }

        _logger.LogDebug("Processing delete operation for entity type {EntityType} through {SinkCount} data sinks", 
            typeof(TEntity).Name, dataSinks.Count);

        var tasks = dataSinks.Select(sink => sink.HandleDeleteAsync(entity, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Flushes all registered data sinks
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        var allDataSinks = _registry.GetAllDataSinks().ToList();
        
        if (!allDataSinks.Any())
        {
            _logger.LogDebug("No data sinks registered for flushing");
            return;
        }

        _logger.LogDebug("Flushing all {SinkCount} registered data sinks", allDataSinks.Count);

        var tasks = allDataSinks.Select(sink => sink.FlushAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Flushes data sinks for a specific entity type
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task FlushAsync<TEntity>(CancellationToken cancellationToken = default)
    {
        var dataSinks = _registry.GetDataSinks<TEntity>().ToList();
        
        if (!dataSinks.Any())
        {
            _logger.LogDebug("No data sinks registered for entity type {EntityType}", typeof(TEntity).Name);
            return;
        }

        _logger.LogDebug("Flushing {SinkCount} data sinks for entity type {EntityType}", 
            dataSinks.Count, typeof(TEntity).Name);

        var tasks = dataSinks.Select(sink => sink.FlushAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }
}
