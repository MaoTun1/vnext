using BBT.Workflow.Instances;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Strategy for managing instance refresh operations to minimize database calls
/// while ensuring data consistency across different service scopes.
/// </summary>
public sealed class InstanceRefreshStrategy(
    IInstanceRepository instanceRepository,
    ILogger<InstanceRefreshStrategy> logger) : IInstanceRefreshStrategy
{
    /// <summary>
    /// Gets the latest instance state with minimal database overhead.
    /// Uses read-only queries for performance optimization.
    /// </summary>
    /// <param name="instanceId">The instance ID to refresh</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The refreshed instance or null if not found</returns>
    public async Task<Instance?> GetLatestInstanceAsync(
        Guid instanceId, 
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Refreshing instance {InstanceId} with read-only query", instanceId);
        
        return await instanceRepository.FindByIdAsReadOnlyAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Checks if an instance needs refresh based on its current state and context.
    /// This helps avoid unnecessary database calls when the instance is already up-to-date.
    /// </summary>
    /// <param name="instance">The current instance</param>
    /// <param name="afterAutoTransition">Whether this check is after an auto transition</param>
    /// <returns>True if refresh is needed, false otherwise</returns>
    public bool ShouldRefreshInstance(Instance instance, bool afterAutoTransition = false)
    {
        // Always refresh after auto transitions as they run in separate scopes
        if (afterAutoTransition)
        {
            logger.LogDebug("Instance {InstanceId} needs refresh after auto transition", instance.Id);
            return true;
        }

        // Refresh if instance is in a transitional state
        if (instance.Status.Equals(InstanceStatus.Busy))
        {
            logger.LogDebug("Instance {InstanceId} needs refresh due to Busy status", instance.Id);
            return true;
        }

        // No refresh needed for completed instances
        if (instance.IsCompleted)
        {
            logger.LogDebug("Instance {InstanceId} is completed, no refresh needed", instance.Id);
        }

        return false;
    }

    /// <summary>
    /// Conditionally refreshes an instance only if needed, reducing unnecessary database calls.
    /// </summary>
    /// <param name="instance">The current instance</param>
    /// <param name="afterAutoTransition">Whether this is after an auto transition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The refreshed instance if refresh was needed, otherwise the original instance</returns>
    public async Task<Instance> RefreshIfNeededAsync(
        Instance instance, 
        bool afterAutoTransition = false,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldRefreshInstance(instance, afterAutoTransition))
        {
            logger.LogDebug("Instance {InstanceId} refresh skipped - not needed", instance.Id);
            return instance;
        }

        var refreshedInstance = await GetLatestInstanceAsync(instance.Id, cancellationToken);
        
        if (refreshedInstance == null)
        {
            logger.LogWarning("Instance {InstanceId} not found during refresh, returning original", instance.Id);
            return instance;
        }

        logger.LogDebug("Instance {InstanceId} successfully refreshed", instance.Id);
        return refreshedInstance;
    }
}
