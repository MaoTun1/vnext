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
    Task<List<InstanceCorrelation>> GetActiveByParentAsync(
        Guid parentInstanceId, 
        CancellationToken cancellationToken = default);
    
    Task<bool> AnyActiveByParentAsync(
        Guid parentInstanceId,
        CancellationToken cancellationToken = default);
    
    Task<InstanceCorrelation?> FindActiveByParentAsync(
        Guid parentInstanceId,
        CancellationToken cancellationToken = default);
} 