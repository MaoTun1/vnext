using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Diagnostics;

namespace BBT.Workflow.Monitoring;

/// <summary>
/// EF Core interceptor that automatically records database metrics for all operations.
/// This provides comprehensive database monitoring without requiring manual metric recording in each repository.
/// </summary>
public sealed class WorkflowDatabaseInterceptor : DbCommandInterceptor
{
    private readonly IWorkflowMetrics _workflowMetrics;

    public WorkflowDatabaseInterceptor(IWorkflowMetrics workflowMetrics)
    {
        _workflowMetrics = workflowMetrics;
    }

    /// <summary>
    /// Intercepts command execution before it starts and records metrics
    /// </summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        var context = CreateExecutionContext(command, eventData);
        
        // Record query start
        _workflowMetrics.RecordDbQuery(context.QueryType, context.TableName, "started");
        
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts command execution after completion and records success metrics
    /// </summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        var context = CreateExecutionContext(command, eventData);
        
        // Record successful query
        _workflowMetrics.RecordDbQuery(context.QueryType, context.TableName, "success");
        _workflowMetrics.RecordDbQueryDuration(context.QueryType, context.TableName, eventData.Duration.TotalSeconds);
        
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts command execution failures and records error metrics
    /// </summary>
    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var context = CreateExecutionContext(command, eventData);
        
        // Record failed query
        _workflowMetrics.RecordDbQuery(context.QueryType, context.TableName, "error");
        _workflowMetrics.RecordDbError(context.QueryType, context.TableName, eventData.Exception.GetType().Name);
        _workflowMetrics.RecordDbQueryDuration(context.QueryType, context.TableName, eventData.Duration.TotalSeconds);
        
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    /// <summary>
    /// Intercepts scalar command execution before it starts
    /// </summary>
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        var context = CreateExecutionContext(command, eventData);
        _workflowMetrics.RecordDbQuery(context.QueryType, context.TableName, "started");
        
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts scalar command execution after completion
    /// </summary>
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        var context = CreateExecutionContext(command, eventData);
        
        _workflowMetrics.RecordDbQuery(context.QueryType, context.TableName, "success");
        _workflowMetrics.RecordDbQueryDuration(context.QueryType, context.TableName, eventData.Duration.TotalSeconds);
        
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts non-query command execution before it starts
    /// </summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = CreateExecutionContext(command, eventData);
        _workflowMetrics.RecordDbQuery(context.QueryType, context.TableName, "started");
        
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts non-query command execution after completion
    /// </summary>
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = CreateExecutionContext(command, eventData);
        
        _workflowMetrics.RecordDbQuery(context.QueryType, context.TableName, "success");
        _workflowMetrics.RecordDbQueryDuration(context.QueryType, context.TableName, eventData.Duration.TotalSeconds);
        
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    // Note: Transaction events require DbTransactionInterceptor, not DbCommandInterceptor
    // For now, we'll focus on command-level metrics. Transaction metrics can be added with a separate interceptor if needed.

    /// <summary>
    /// Creates execution context from command and event data for metrics recording
    /// </summary>
    private static DatabaseExecutionContext CreateExecutionContext(DbCommand command, CommandEventData eventData)
    {
        var sql = command.CommandText?.Trim() ?? string.Empty;
        var queryType = ExtractQueryType(sql);
        var tableName = ExtractTableName(sql, queryType);
        
        return new DatabaseExecutionContext
        {
            QueryType = queryType,
            TableName = tableName,
            CommandText = sql
        };
    }

    /// <summary>
    /// Extracts the query type (SELECT, INSERT, UPDATE, DELETE) from SQL command text
    /// </summary>
    private static string ExtractQueryType(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "Unknown";

        var trimmedSql = sql.Trim().ToUpperInvariant();
        
        if (trimmedSql.StartsWith("SELECT"))
            return "SELECT";
        if (trimmedSql.StartsWith("INSERT"))
            return "INSERT";
        if (trimmedSql.StartsWith("UPDATE"))
            return "UPDATE";
        if (trimmedSql.StartsWith("DELETE"))
            return "DELETE";
        if (trimmedSql.StartsWith("CREATE"))
            return "CREATE";
        if (trimmedSql.StartsWith("ALTER"))
            return "ALTER";
        if (trimmedSql.StartsWith("DROP"))
            return "DROP";
        
        return "Other";
    }

    /// <summary>
    /// Extracts the primary table name from SQL command text
    /// </summary>
    private static string ExtractTableName(string sql, string queryType)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "Unknown";

        try
        {
            var upperSql = sql.ToUpperInvariant();
            
            return queryType switch
            {
                "SELECT" => ExtractTableFromSelect(upperSql),
                "INSERT" => ExtractTableFromInsert(upperSql),
                "UPDATE" => ExtractTableFromUpdate(upperSql),
                "DELETE" => ExtractTableFromDelete(upperSql),
                _ => "Multiple"
            };
        }
        catch
        {
            // If parsing fails, return a safe default
            return "Unknown";
        }
    }

    private static string ExtractTableFromSelect(string sql)
    {
        var fromIndex = sql.IndexOf(" FROM ", StringComparison.Ordinal);
        if (fromIndex == -1) return "Unknown";
        
        var afterFrom = sql.Substring(fromIndex + 6).Trim();
        var words = afterFrom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length > 0)
        {
            var tableName = words[0].Trim('[', ']', '"', '`');
            // Remove schema prefix if present
            var dotIndex = tableName.LastIndexOf('.');
            if (dotIndex > 0)
                tableName = tableName.Substring(dotIndex + 1);
            return tableName;
        }
        
        return "Unknown";
    }

    private static string ExtractTableFromInsert(string sql)
    {
        var intoIndex = sql.IndexOf(" INTO ", StringComparison.Ordinal);
        if (intoIndex == -1) return "Unknown";
        
        var afterInto = sql.Substring(intoIndex + 6).Trim();
        var words = afterInto.Split(' ', '(', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length > 0)
        {
            var tableName = words[0].Trim('[', ']', '"', '`');
            var dotIndex = tableName.LastIndexOf('.');
            if (dotIndex > 0)
                tableName = tableName.Substring(dotIndex + 1);
            return tableName;
        }
        
        return "Unknown";
    }

    private static string ExtractTableFromUpdate(string sql)
    {
        var updateIndex = sql.IndexOf("UPDATE ", StringComparison.Ordinal);
        if (updateIndex == -1) return "Unknown";
        
        var afterUpdate = sql.Substring(updateIndex + 7).Trim();
        var words = afterUpdate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length > 0)
        {
            var tableName = words[0].Trim('[', ']', '"', '`');
            var dotIndex = tableName.LastIndexOf('.');
            if (dotIndex > 0)
                tableName = tableName.Substring(dotIndex + 1);
            return tableName;
        }
        
        return "Unknown";
    }

    private static string ExtractTableFromDelete(string sql)
    {
        var fromIndex = sql.IndexOf(" FROM ", StringComparison.Ordinal);
        if (fromIndex == -1) return "Unknown";
        
        var afterFrom = sql.Substring(fromIndex + 6).Trim();
        var words = afterFrom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length > 0)
        {
            var tableName = words[0].Trim('[', ']', '"', '`');
            var dotIndex = tableName.LastIndexOf('.');
            if (dotIndex > 0)
                tableName = tableName.Substring(dotIndex + 1);
            return tableName;
        }
        
        return "Unknown";
    }
}

/// <summary>
/// Context information for database operation execution
/// </summary>
internal record DatabaseExecutionContext
{
    public required string QueryType { get; init; }
    public required string TableName { get; init; }
    public required string CommandText { get; init; }
}