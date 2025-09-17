using BBT.Workflow.ClickHouse;
using BBT.Workflow.Instances;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BBT.Workflow.Infrastructure.ClickHouse;

/// <summary>
/// ClickHouse data transfer service implementation
/// </summary>
public class ClickHouseDataTransferService : IClickHouseDataTransfer, IDisposable
{
    private readonly ClickHouseConfiguration _configuration;
    private readonly ILogger<ClickHouseDataTransferService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<ClickHouseInstanceData> _instanceQueue;
    private readonly ConcurrentQueue<ClickHouseInstanceTransitionData> _transitionQueue;
    private readonly ConcurrentQueue<ClickHouseInstanceTaskData> _taskQueue;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushSemaphore;
    private bool _disposed = false;

    public ClickHouseDataTransferService(
        IConfiguration configuration,
        ILogger<ClickHouseDataTransferService> logger,
        HttpClient httpClient)
    {
        _configuration = configuration.GetSection("ClickHouse").Get<ClickHouseConfiguration>() ?? new ClickHouseConfiguration();
        _logger = logger;
        _httpClient = httpClient;
        
        _instanceQueue = new ConcurrentQueue<ClickHouseInstanceData>();
        _transitionQueue = new ConcurrentQueue<ClickHouseInstanceTransitionData>();
        _taskQueue = new ConcurrentQueue<ClickHouseInstanceTaskData>();
        
        _flushSemaphore = new SemaphoreSlim(1, 1);
        
        // Setup periodic flush timer
        _flushTimer = new Timer(FlushTimerCallback, null, 
            TimeSpan.FromSeconds(_configuration.FlushIntervalSeconds), 
            TimeSpan.FromSeconds(_configuration.FlushIntervalSeconds));

        _logger.LogInformation("ClickHouse data transfer service initialized. Enabled: {Enabled}", _configuration.Enabled);
    }

    /// <summary>
    /// Transfers instance data to ClickHouse
    /// </summary>
    public async Task TransferInstanceAsync(Instance instance, DataTransferOperation operation, CancellationToken cancellationToken = default)
    {
        if (!_configuration.Enabled)
        {
            return;
        }

        try
        {
            var data = MapInstanceToClickHouse(instance, operation);
            _instanceQueue.Enqueue(data);
            
            _logger.LogDebug("Queued instance {InstanceId} for ClickHouse transfer. Operation: {Operation}", 
                instance.Id, operation);

            // Check if we need to flush immediately
            if (_instanceQueue.Count >= _configuration.BatchSize)
            {
                await FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue instance {InstanceId} for ClickHouse transfer", instance.Id);
        }
    }

    /// <summary>
    /// Transfers instance transition data to ClickHouse
    /// </summary>
    public async Task TransferInstanceTransitionAsync(InstanceTransition transition, DataTransferOperation operation, CancellationToken cancellationToken = default)
    {
        if (!_configuration.Enabled)
        {
            return;
        }

        try
        {
            var data = MapInstanceTransitionToClickHouse(transition, operation);
            _transitionQueue.Enqueue(data);
            
            _logger.LogDebug("Queued instance transition {TransitionId} for ClickHouse transfer. Operation: {Operation}", 
                transition.Id, operation);

            // Check if we need to flush immediately
            if (_transitionQueue.Count >= _configuration.BatchSize)
            {
                await FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue instance transition {TransitionId} for ClickHouse transfer", transition.Id);
        }
    }

    /// <summary>
    /// Transfers instance task data to ClickHouse
    /// </summary>
    public async Task TransferInstanceTaskAsync(InstanceTask task, DataTransferOperation operation, CancellationToken cancellationToken = default)
    {
        if (!_configuration.Enabled)
        {
            return;
        }

        try
        {
            var data = MapInstanceTaskToClickHouse(task, operation);
            _taskQueue.Enqueue(data);
            
            _logger.LogDebug("Queued instance task {TaskId} for ClickHouse transfer. Operation: {Operation}", 
                task.Id, operation);

            // Check if we need to flush immediately
            if (_taskQueue.Count >= _configuration.BatchSize)
            {
                await FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue instance task {TaskId} for ClickHouse transfer", task.Id);
        }
    }

    /// <summary>
    /// Flushes any pending data to ClickHouse
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.Enabled)
        {
            return;
        }

        await _flushSemaphore.WaitAsync(cancellationToken);
        try
        {
            await FlushInstancesAsync(cancellationToken);
            await FlushTransitionsAsync(cancellationToken);
            await FlushTasksAsync(cancellationToken);
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Timer callback for periodic flushing
    /// </summary>
    private async void FlushTimerCallback(object? state)
    {
        try
        {
            await FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic ClickHouse flush");
        }
    }

    /// <summary>
    /// Flushes instance data to ClickHouse
    /// </summary>
    private async Task FlushInstancesAsync(CancellationToken cancellationToken)
    {
        if (_instanceQueue.IsEmpty)
        {
            return;
        }

        var instances = new List<ClickHouseInstanceData>();
        while (_instanceQueue.TryDequeue(out var instance))
        {
            instances.Add(instance);
        }

        if (instances.Count > 0)
        {
            await SendDataToClickHouseAsync(_configuration.Tables.Instances, instances, cancellationToken);
            _logger.LogDebug("Flushed {Count} instances to ClickHouse", instances.Count);
        }
    }

    /// <summary>
    /// Flushes transition data to ClickHouse
    /// </summary>
    private async Task FlushTransitionsAsync(CancellationToken cancellationToken)
    {
        if (_transitionQueue.IsEmpty)
        {
            return;
        }

        var transitions = new List<ClickHouseInstanceTransitionData>();
        while (_transitionQueue.TryDequeue(out var transition))
        {
            transitions.Add(transition);
        }

        if (transitions.Count > 0)
        {
            await SendDataToClickHouseAsync(_configuration.Tables.InstanceTransitions, transitions, cancellationToken);
            _logger.LogDebug("Flushed {Count} transitions to ClickHouse", transitions.Count);
        }
    }

    /// <summary>
    /// Flushes task data to ClickHouse
    /// </summary>
    private async Task FlushTasksAsync(CancellationToken cancellationToken)
    {
        if (_taskQueue.IsEmpty)
        {
            return;
        }

        var tasks = new List<ClickHouseInstanceTaskData>();
        while (_taskQueue.TryDequeue(out var task))
        {
            tasks.Add(task);
        }

        if (tasks.Count > 0)
        {
            await SendDataToClickHouseAsync(_configuration.Tables.InstanceTasks, tasks, cancellationToken);
            _logger.LogDebug("Flushed {Count} tasks to ClickHouse", tasks.Count);
        }
    }

    /// <summary>
    /// Sends data to ClickHouse via HTTP interface
    /// </summary>
    private async Task SendDataToClickHouseAsync<T>(string tableName, List<T> data, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        while (retryCount < _configuration.RetryAttempts)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var url = $"{_configuration.ConnectionString}/?query=INSERT INTO {tableName} FORMAT JSONEachRow";
                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully sent {Count} records to ClickHouse table {TableName}", 
                        data.Count, tableName);
                    return;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException($"ClickHouse request failed with status {response.StatusCode}: {errorContent}");
                }
            }
            catch (Exception ex) when (retryCount < _configuration.RetryAttempts - 1)
            {
                retryCount++;
                _logger.LogWarning(ex, "ClickHouse request failed, retrying {RetryCount}/{MaxRetries}", 
                    retryCount, _configuration.RetryAttempts);
                
                await Task.Delay(_configuration.RetryDelayMilliseconds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send data to ClickHouse table {TableName} after {RetryCount} attempts", 
                    tableName, _configuration.RetryAttempts);
                throw;
            }
        }
    }

    /// <summary>
    /// Maps Instance to ClickHouse data model
    /// </summary>
    private static ClickHouseInstanceData MapInstanceToClickHouse(Instance instance, DataTransferOperation operation)
    {
        return new ClickHouseInstanceData
        {
            Id = instance.Id,
            Key = instance.Key,
            Flow = instance.Flow,
            CurrentState = instance.CurrentState,
            Status = instance.Status.Code,
            CreatedAt = instance.CreatedAt,
            ModifiedAt = instance.ModifiedAt,
            CompletedAt = instance.CompletedAt,
            DurationSeconds = instance.Duration?.TotalSeconds,
            Tags = JsonSerializer.Serialize(instance.Tags),
            IsTransient = instance.IsTransient,
            Operation = operation.ToString(),
            TransferTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps InstanceTransition to ClickHouse data model
    /// </summary>
    private static ClickHouseInstanceTransitionData MapInstanceTransitionToClickHouse(InstanceTransition transition, DataTransferOperation operation)
    {
        return new ClickHouseInstanceTransitionData
        {
            Id = transition.Id,
            InstanceId = transition.InstanceId,
            TransitionId = transition.TransitionId,
            FromState = transition.FromState,
            ToState = transition.ToState,
            StartedAt = transition.StartedAt,
            FinishedAt = transition.FinishedAt,
            DurationSeconds = transition.Duration?.TotalSeconds,
            Body = transition.Body.Json,
            Header = transition.Header.Json,
            Operation = operation.ToString(),
            TransferTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps InstanceTask to ClickHouse data model
    /// </summary>
    private static ClickHouseInstanceTaskData MapInstanceTaskToClickHouse(InstanceTask task, DataTransferOperation operation)
    {
        return new ClickHouseInstanceTaskData
        {
            Id = task.Id,
            TransitionId = task.TransitionId,
            TaskId = task.TaskId,
            Status = task.Status.ToString(),
            StartedAt = task.StartedAt,
            FinishedAt = task.FinishedAt,
            DurationSeconds = task.Duration?.TotalSeconds,
            FaultedTaskId = task.FaultedTaskId,
            Request = task.Request.Json,
            Response = task.Response.Json,
            Operation = operation.ToString(),
            TransferTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Disposes the service
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _flushTimer?.Dispose();
            _flushSemaphore?.Dispose();
            _disposed = true;
        }
    }
}
