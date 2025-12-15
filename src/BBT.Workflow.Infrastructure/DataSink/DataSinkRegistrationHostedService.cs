using BBT.Workflow.DataSink;
using BBT.Workflow.Instances;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.DataSink;

/// <summary>
/// Hosted service that registers all DataSink implementations with the registry
/// </summary>
public class DataSinkRegistrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataSinkRegistrationHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the DataSinkRegistrationHostedService class
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="logger">Logger instance</param>
    public DataSinkRegistrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<DataSinkRegistrationHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the hosted service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DataSink registration service");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IDataSinkRegistry>();

            // Register Instance DataSinks
            var instanceDataSinks = scope.ServiceProvider.GetServices<IDataSink<Instance>>();
            foreach (var dataSink in instanceDataSinks)
            {
                registry.Register(dataSink);
                _logger.LogInformation("Registered DataSink {DataSinkName} for entity type {EntityType}", 
                    dataSink.Name, typeof(Instance).Name);
            }

            // Register InstanceTransition DataSinks
            var transitionDataSinks = scope.ServiceProvider.GetServices<IDataSink<InstanceTransition>>();
            foreach (var dataSink in transitionDataSinks)
            {
                registry.Register(dataSink);
                _logger.LogInformation("Registered DataSink {DataSinkName} for entity type {EntityType}", 
                    dataSink.Name, typeof(InstanceTransition).Name);
            }

            // Register InstanceTask DataSinks
            var taskDataSinks = scope.ServiceProvider.GetServices<IDataSink<InstanceTask>>();
            foreach (var dataSink in taskDataSinks)
            {
                registry.Register(dataSink);
                _logger.LogInformation("Registered DataSink {DataSinkName} for entity type {EntityType}", 
                    dataSink.Name, typeof(InstanceTask).Name);
            }

            _logger.LogInformation("DataSink registration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register DataSinks");
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the hosted service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DataSink registration service");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IDataSinkRegistry>();

            // Flush all data sinks before stopping
            var allDataSinks = registry.GetAllDataSinks();
            var flushTasks = allDataSinks.Select(sink => sink.FlushAsync(cancellationToken));
            await Task.WhenAll(flushTasks);

            _logger.LogInformation("DataSink registration service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while stopping DataSink registration service");
        }
    }
}
