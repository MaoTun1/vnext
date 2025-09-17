using BBT.Workflow.Instances;

namespace BBT.Workflow.ClickHouse;

/// <summary>
/// Interface for ClickHouse data transfer operations
/// </summary>
public interface IClickHouseDataTransfer
{
    /// <summary>
    /// Transfers instance data to ClickHouse
    /// </summary>
    /// <param name="instance">Instance entity</param>
    /// <param name="operation">Operation type (Insert/Update)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task TransferInstanceAsync(Instance instance, DataTransferOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers instance transition data to ClickHouse
    /// </summary>
    /// <param name="transition">Instance transition entity</param>
    /// <param name="operation">Operation type (Insert/Update)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task TransferInstanceTransitionAsync(InstanceTransition transition, DataTransferOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers instance task data to ClickHouse
    /// </summary>
    /// <param name="task">Instance task entity</param>
    /// <param name="operation">Operation type (Insert/Update)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task TransferInstanceTaskAsync(InstanceTask task, DataTransferOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending data to ClickHouse
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Data transfer operation types
/// </summary>
public enum DataTransferOperation
{
    /// <summary>
    /// Insert operation
    /// </summary>
    Insert,

    /// <summary>
    /// Update operation
    /// </summary>
    Update
}

