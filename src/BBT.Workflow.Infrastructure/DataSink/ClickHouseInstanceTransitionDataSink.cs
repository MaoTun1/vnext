using BBT.Workflow.ClickHouse;
using BBT.Workflow.DataSink;
using BBT.Workflow.Instances;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Infrastructure.DataSink;

/// <summary>
/// ClickHouse data sink implementation for InstanceTransition entities
/// </summary>
public class ClickHouseInstanceTransitionDataSink : AbstractDataSink<InstanceTransition>, IDataSink
{
    private readonly ClickHouseConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<ClickHouseInstanceTransitionData> _queue;
    private readonly SemaphoreSlim _flushSemaphore;
    private readonly Timer _flushTimer;

    /// <summary>
    /// Initializes a new instance of the ClickHouseInstanceTransitionDataSink class
    /// </summary>
    /// <param name="configuration">ClickHouse configuration</param>
    /// <param name="httpClient">HTTP client for ClickHouse communication</param>
    /// <param name="logger">Logger instance</param>
    public ClickHouseInstanceTransitionDataSink(
        IOptions<ClickHouseConfiguration> configuration,
        HttpClient httpClient,
        ILogger<ClickHouseInstanceTransitionDataSink> logger)
        : base(logger)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _queue = new ConcurrentQueue<ClickHouseInstanceTransitionData>();
        _flushSemaphore = new SemaphoreSlim(1, 1);

        // Setup periodic flush timer
        _flushTimer = new Timer(async _ => await FlushTimerCallback(), null,
            TimeSpan.FromSeconds(_configuration.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_configuration.FlushIntervalSeconds));
    }

    /// <summary>
    /// Gets the name of this data sink
    /// </summary>
    public override string Name => "ClickHouse-InstanceTransition";

    /// <summary>
    /// Gets whether this data sink is enabled
    /// </summary>
    public override bool IsEnabled => _configuration.Enabled;

    /// <summary>
    /// Called when an insert operation occurs
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected override async Task OnInsertAsync(InstanceTransition entity, CancellationToken cancellationToken)
    {
        var data = MapInstanceTransitionToClickHouse(entity, DataSinkOperation.Insert);
        _queue.Enqueue(data);

        Logger.LogDebug("Queued instance transition {TransitionId} for ClickHouse transfer. Operation: Insert", entity.Id);

        // Check if we need to flush immediately
        if (_queue.Count >= _configuration.BatchSize)
        {
            await FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Called when an update operation occurs
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected override async Task OnUpdateAsync(InstanceTransition entity, CancellationToken cancellationToken)
    {
        var data = MapInstanceTransitionToClickHouse(entity, DataSinkOperation.Update);
        _queue.Enqueue(data);

        Logger.LogDebug("Queued instance transition {TransitionId} for ClickHouse transfer. Operation: Update", entity.Id);

        // Check if we need to flush immediately
        if (_queue.Count >= _configuration.BatchSize)
        {
            await FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Called when a delete operation occurs
    /// </summary>
    /// <param name="entity">The entity to transfer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected override async Task OnDeleteAsync(InstanceTransition entity, CancellationToken cancellationToken)
    {
        var data = MapInstanceTransitionToClickHouse(entity, DataSinkOperation.Delete);
        _queue.Enqueue(data);

        Logger.LogDebug("Queued instance transition {TransitionId} for ClickHouse transfer. Operation: Delete", entity.Id);

        // Check if we need to flush immediately
        if (_queue.Count >= _configuration.BatchSize)
        {
            await FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Called when flushing pending data
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    protected override async Task OnFlushAsync(CancellationToken cancellationToken)
    {
        await _flushSemaphore.WaitAsync(cancellationToken);
        try
        {
            var dataToFlush = new List<ClickHouseInstanceTransitionData>();

            // Dequeue all items
            while (_queue.TryDequeue(out var item))
            {
                dataToFlush.Add(item);
            }

            if (dataToFlush.Any())
            {
                await SendDataToClickHouseAsync(_configuration.Tables.InstanceTransitions, dataToFlush, cancellationToken);
                Logger.LogDebug("Flushed {Count} instance transition records to ClickHouse", dataToFlush.Count);
            }
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Flush timer callback
    /// </summary>
    private async Task FlushTimerCallback()
    {
        try
        {
            await OnFlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during scheduled flush in ClickHouse instance transition data sink");
        }
    }

    /// <summary>
    /// Sends data to ClickHouse
    /// </summary>
    /// <param name="tableName">Target table name</param>
    /// <param name="data">Data to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task SendDataToClickHouseAsync(string tableName, List<ClickHouseInstanceTransitionData> data, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        while (retryCount < _configuration.RetryAttempts)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null, // Keep PascalCase for ClickHouse compatibility
                    WriteIndented = false
                };

                // Custom converters for DateTime and Guid fields to ClickHouse format
                jsonOptions.Converters.Add(new ClickHouseDateTimeConverter());
                jsonOptions.Converters.Add(new ClickHouseNullableDateTimeConverter());
                jsonOptions.Converters.Add(new ClickHouseGuidConverter());
                jsonOptions.Converters.Add(new ClickHouseNullableGuidConverter());

                var json = JsonSerializer.Serialize(data, jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var baseUrl = ExtractBaseUrlFromConnectionString(_configuration.ConnectionString);
                if (string.IsNullOrEmpty(baseUrl))
                {
                    throw new InvalidOperationException("Invalid ClickHouse connection string. Could not extract base URL.");
                }

                var database = ExtractDatabaseFromConnectionString(_configuration.ConnectionString);
                var fullTableName = !string.IsNullOrEmpty(database) ? $"{database}.{tableName}" : tableName;

                var url = $"{baseUrl}/?query=INSERT INTO {fullTableName} FORMAT JSONEachRow";
                var response = await _httpClient.PostAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    Logger.LogDebug("Successfully sent {Count} instance transition records to ClickHouse table {TableName}",
                        data.Count, tableName);
                    return;
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"ClickHouse request failed with status {response.StatusCode}: {errorContent}");
            }
            catch (Exception ex) when (retryCount < _configuration.RetryAttempts - 1)
            {
                retryCount++;
                Logger.LogWarning(ex, "ClickHouse request failed, retrying {RetryCount}/{MaxRetries}",
                    retryCount, _configuration.RetryAttempts);

                await Task.Delay(_configuration.RetryDelayMilliseconds, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send instance transition data to ClickHouse table {TableName} after {RetryCount} attempts",
                    tableName, _configuration.RetryAttempts);
                throw;
            }
        }
    }

    /// <summary>
    /// Maps InstanceTransition to ClickHouse data model
    /// </summary>
    /// <param name="transition">Instance transition entity</param>
    /// <param name="operation">Data transfer operation</param>
    /// <returns>ClickHouse instance transition data</returns>
    private static ClickHouseInstanceTransitionData MapInstanceTransitionToClickHouse(InstanceTransition transition, DataSinkOperation operation)
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
            Body = JsonSerializer.Serialize(transition.Body),
            Header = JsonSerializer.Serialize(transition.Header),
            Operation = operation.ToString(),
            TransferTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Extracts base URL from ClickHouse connection string
    /// </summary>
    /// <param name="connectionString">ClickHouse connection string</param>
    /// <returns>Base URL for ClickHouse HTTP interface</returns>
    private static string? ExtractBaseUrlFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return null;
        }

        // Parse connection string format: Host=localhost;Port=8123;Database=workflow_analytics;Username=default;Password=;
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        string? host = null;
        string? port = null;

        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                switch (key.ToLowerInvariant())
                {
                    case "host":
                        host = value;
                        break;
                    case "port":
                        port = value;
                        break;
                    default:

                        break;
                }
            }
        }

        if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(port))
        {
            return $"http://{host}:{port}";
        }

        return null;
    }

    /// <summary>
    /// Extracts database name from ClickHouse connection string
    /// </summary>
    /// <param name="connectionString">ClickHouse connection string</param>
    /// <returns>Database name</returns>
    private static string? ExtractDatabaseFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return null;
        }

        // Parse connection string format: Host=localhost;Port=8123;Database=workflow_analytics;Username=default;Password=;
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                if (key.ToLowerInvariant() == "database")
                {
                    return value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Disposes the data sink
    /// </summary>
    public void Dispose()
    {
        _flushTimer?.Dispose();
        _flushSemaphore?.Dispose();
    }
}
