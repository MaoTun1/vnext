using BBT.Aether.Domain.Repositories;

namespace BBT.Workflow.Instances;

/// <summary>
/// Repository interface for managing workflow instance correlations.
/// This interface provides data access methods for handling parent-child relationships
/// between workflow instances, particularly for SubFlow and SubProcess scenarios.
/// </summary>
public interface IInstanceCorrelationRepository : IRepository<InstanceCorrelation, Guid>
{
    /// <summary>
    /// Finds active correlations where the specified instance ID is the parent instance.
    /// This method is used to identify all active child flows for a given parent workflow.
    /// </summary>
    /// <param name="parentInstanceId">The unique identifier of the parent workflow instance.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The result contains a collection of active correlations where the instance is a parent.
    /// </returns>
    Task<List<InstanceCorrelation>> FindActiveByParentInstanceIdAsync(
        Guid parentInstanceId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the parent correlation for the specified sub-flow instance.
    /// This method is used to identify the parent workflow when a sub-flow needs to signal completion.
    /// </summary>
    /// <param name="subFlowInstanceId">The unique identifier of the sub-flow instance.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The result contains the correlation record if found; otherwise, null.
    /// </returns>
    Task<InstanceCorrelation?> FindBySubFlowInstanceIdAsync(
        Guid subFlowInstanceId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all incomplete correlations for the specified parent instance and state.
    /// This method is used to check if there are any pending sub-flows that block the parent workflow.
    /// </summary>
    /// <param name="parentInstanceId">The unique identifier of the parent workflow instance.</param>
    /// <param name="parentState">The state key where the sub-flows were initiated.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The result contains a collection of incomplete correlations for the specified criteria.
    /// </returns>
    Task<List<InstanceCorrelation>> FindIncompleteByParentAsync(
        Guid parentInstanceId, 
        string parentState, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there are any incomplete correlations for the specified parent instance.
    /// This method provides a quick way to determine if a parent workflow has pending sub-flows.
    /// </summary>
    /// <param name="parentInstanceId">The unique identifier of the parent workflow instance.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The result is true if there are incomplete correlations; otherwise, false.
    /// </returns>
    Task<bool> HasIncompleteCorrelationsAsync(
        Guid parentInstanceId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there are any active blocking SubFlow instances for the specified parent instance.
    /// This method performs a database-level join query to check for SubFlow type "S" instances
    /// without fetching individual instance records for better performance.
    /// </summary>
    /// <param name="parentInstanceId">The unique identifier of the parent workflow instance.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The result is true if there are active blocking SubFlow instances; otherwise, false.
    /// </returns>
    Task<bool> HasActiveBlockingSubFlowsAsync(
        Guid parentInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the first active blocking SubFlow correlation for the specified parent instance and state.
    /// This method is optimized to directly return blocking SubFlow (Type "S") correlations without additional filtering.
    /// </summary>
    /// <param name="parentInstanceId">The unique identifier of the parent workflow instance.</param>
    /// <param name="parentState">The state key where the sub-flow was initiated.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The result contains the first active blocking SubFlow correlation if found; otherwise, null.
    /// </returns>
    Task<InstanceCorrelation?> FindActiveBlockingSubFlowAsync(
        Guid parentInstanceId,
        string parentState,
        CancellationToken cancellationToken = default);
} 