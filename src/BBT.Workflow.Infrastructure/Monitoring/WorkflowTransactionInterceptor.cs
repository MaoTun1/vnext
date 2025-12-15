using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace BBT.Workflow.Monitoring;

/// <summary>
/// EF Core transaction interceptor that automatically records transaction metrics.
/// This complements WorkflowDatabaseInterceptor by focusing specifically on transaction-level operations.
/// </summary>
public sealed class WorkflowTransactionInterceptor : DbTransactionInterceptor
{
    private readonly IWorkflowMetrics _workflowMetrics;

    public WorkflowTransactionInterceptor(IWorkflowMetrics workflowMetrics)
    {
        _workflowMetrics = workflowMetrics;
    }

    /// <summary>
    /// Intercepts transaction start events
    /// </summary>
    public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
        DbConnection connection,
        TransactionStartingEventData eventData,
        InterceptionResult<DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        _workflowMetrics.RecordDbConnection("transaction", "started");
        
        return base.TransactionStartingAsync(connection, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts transaction committed events
    /// </summary>
    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _workflowMetrics.RecordDbConnection("transaction", "committed");
        _workflowMetrics.RecordDbTransactionDuration("commit", eventData.Duration.TotalSeconds);
        
        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    /// <summary>
    /// Intercepts transaction rolled back events
    /// </summary>
    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _workflowMetrics.RecordDbConnection("transaction", "rollback");
        _workflowMetrics.RecordDbTransactionDuration("rollback", eventData.Duration.TotalSeconds);
        
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    /// <summary>
    /// Intercepts transaction failure events
    /// </summary>
    public override Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _workflowMetrics.RecordDbConnection("transaction", "error");
        _workflowMetrics.RecordDbError("TRANSACTION", "transaction", eventData.Exception.GetType().Name);
        _workflowMetrics.RecordDbTransactionDuration("error", eventData.Duration.TotalSeconds);
        
        return base.TransactionFailedAsync(transaction, eventData, cancellationToken);
    }
}