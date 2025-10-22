using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Telemetry;

/// <summary>
/// Source-generated high-performance logging methods for infrastructure operations.
/// Covers database, cache, HTTP, and external service interactions.
/// </summary>
public static partial class InfrastructureLogs
{
    #region Database Operations

    /// <summary>
    /// Logs when a database query is executed.
    /// </summary>
    [LoggerMessage(
        EventId = 30001,
        Level = LogLevel.Debug,
        Message = "{Prefix} Database query executed: {QueryType} in {ElapsedMs}ms")]
    public static partial void DatabaseQueryExecuted(
        this ILogger logger,
        string prefix,
        string queryType,
        long elapsedMs);

    /// <summary>
    /// Logs when a database query is slow.
    /// </summary>
    [LoggerMessage(
        EventId = 30040,
        Level = LogLevel.Warning,
        Message = "{Prefix} Slow database query detected: {QueryType} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)")]
    public static partial void DatabaseSlowQuery(
        this ILogger logger,
        string prefix,
        string queryType,
        long elapsedMs,
        int thresholdMs);

    /// <summary>
    /// Logs when database operation is retrying.
    /// </summary>
    [LoggerMessage(
        EventId = 30041,
        Level = LogLevel.Warning,
        Message = "{Prefix} Database operation retrying (attempt {Attempt}): {Operation}")]
    public static partial void DatabaseRetrying(
        this ILogger logger,
        string prefix,
        int attempt,
        string operation);

    /// <summary>
    /// Logs when a database connection fails.
    /// </summary>
    [LoggerMessage(
        EventId = 30070,
        Level = LogLevel.Error,
        Message = "{Prefix} Database connection failed to {ConnectionString}")]
    public static partial void DatabaseConnectionFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string connectionString);

    /// <summary>
    /// Logs when a database query fails.
    /// </summary>
    [LoggerMessage(
        EventId = 30071,
        Level = LogLevel.Error,
        Message = "{Prefix} Database query failed: {QueryType}")]
    public static partial void DatabaseQueryFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string queryType);

    #endregion

    #region Cache Operations

    /// <summary>
    /// Logs when a cache hit occurs.
    /// </summary>
    [LoggerMessage(
        EventId = 30002,
        Level = LogLevel.Debug,
        Message = "{Prefix} Cache hit for key: {CacheKey}")]
    public static partial void CacheHit(
        this ILogger logger,
        string prefix,
        string cacheKey);

    /// <summary>
    /// Logs when a cache miss occurs.
    /// </summary>
    [LoggerMessage(
        EventId = 30003,
        Level = LogLevel.Debug,
        Message = "{Prefix} Cache miss for key: {CacheKey}")]
    public static partial void CacheMiss(
        this ILogger logger,
        string prefix,
        string cacheKey);

    /// <summary>
    /// Logs when a cache error occurs.
    /// </summary>
    [LoggerMessage(
        EventId = 30042,
        Level = LogLevel.Warning,
        Message = "{Prefix} Cache error for key {CacheKey}: {Message}")]
    public static partial void CacheError(
        this ILogger logger,
        string prefix,
        string cacheKey,
        string message);

    #endregion

    #region HTTP Operations

    /// <summary>
    /// Logs when an HTTP request is sent.
    /// </summary>
    [LoggerMessage(
        EventId = 30004,
        Level = LogLevel.Debug,
        Message = "{Prefix} HTTP {Method} request sent to {Url}")]
    public static partial void HttpRequestSent(
        this ILogger logger,
        string prefix,
        string method,
        string url);

    /// <summary>
    /// Logs when an HTTP response is received.
    /// </summary>
    [LoggerMessage(
        EventId = 30005,
        Level = LogLevel.Debug,
        Message = "{Prefix} HTTP response received: {StatusCode} from {Url} in {ElapsedMs}ms")]
    public static partial void HttpResponseReceived(
        this ILogger logger,
        string prefix,
        int statusCode,
        string url,
        long elapsedMs);

    /// <summary>
    /// Logs when an HTTP request times out.
    /// </summary>
    [LoggerMessage(
        EventId = 30043,
        Level = LogLevel.Warning,
        Message = "{Prefix} HTTP request timeout to {Url} after {TimeoutMs}ms")]
    public static partial void HttpRequestTimeout(
        this ILogger logger,
        string prefix,
        string url,
        int timeoutMs);

    /// <summary>
    /// Logs when an HTTP request is retrying.
    /// </summary>
    [LoggerMessage(
        EventId = 30044,
        Level = LogLevel.Warning,
        Message = "{Prefix} HTTP request retrying (attempt {Attempt}) to {Url}")]
    public static partial void HttpRequestRetrying(
        this ILogger logger,
        string prefix,
        int attempt,
        string url);

    /// <summary>
    /// Logs when an HTTP request fails.
    /// </summary>
    [LoggerMessage(
        EventId = 30072,
        Level = LogLevel.Error,
        Message = "{Prefix} HTTP {Method} request failed to {Url}")]
    public static partial void HttpRequestFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string method,
        string url);

    #endregion

    #region ClickHouse Operations

    /// <summary>
    /// Logs when a batch is inserted into ClickHouse.
    /// </summary>
    [LoggerMessage(
        EventId = 30006,
        Level = LogLevel.Information,
        Message = "{Prefix} ClickHouse batch inserted: {RecordCount} records to {Table} in {ElapsedMs}ms")]
    public static partial void ClickHouseBatchInserted(
        this ILogger logger,
        string prefix,
        int recordCount,
        string table,
        long elapsedMs);

    /// <summary>
    /// Logs when ClickHouse flush encounters a warning.
    /// </summary>
    [LoggerMessage(
        EventId = 30045,
        Level = LogLevel.Warning,
        Message = "{Prefix} ClickHouse flush warning for table {Table}: {Message}")]
    public static partial void ClickHouseFlushWarning(
        this ILogger logger,
        string prefix,
        string table,
        string message);

    /// <summary>
    /// Logs when ClickHouse insert fails.
    /// </summary>
    [LoggerMessage(
        EventId = 30073,
        Level = LogLevel.Error,
        Message = "{Prefix} ClickHouse insert failed for table {Table}")]
    public static partial void ClickHouseInsertFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string table);

    #endregion

    #region Schema Operations

    /// <summary>
    /// Logs when database schema changes.
    /// </summary>
    [LoggerMessage(
        EventId = 30007,
        Level = LogLevel.Information,
        Message = "{Prefix} Database schema changed to {SchemaName}")]
    public static partial void SchemaChanged(
        this ILogger logger,
        string prefix,
        string schemaName);

    #endregion
}

