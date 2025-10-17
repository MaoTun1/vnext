using BBT.Aether;
using BBT.Aether.DistributedLock;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Domain.Extensions;

/// <summary>
/// Extension methods for IDistributedLockService to provide cleaner lock management patterns.
/// </summary>
public static class DistributedLockServiceExtensions
{
    /// <summary>
    /// Executes an action within a distributed lock context, automatically handling lock acquisition and release.
    /// </summary>
    /// <param name="distributedLockService">The distributed lock service instance.</param>
    /// <param name="resourceId">The unique identifier for the resource to lock.</param>
    /// <param name="lockExpiryInSeconds">The lock expiry time in seconds.</param>
    /// <param name="action">The action to execute within the lock context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <param name="logger">Optional logger for lock operation warnings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DistributedLockAcquisitionException">Thrown when the lock cannot be acquired.</exception>
    public static async Task ExecuteWithLockAsync(
        this IDistributedLockService distributedLockService,
        string resourceId,
        int lockExpiryInSeconds,
        Func<Task> action,
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        var lockAcquired = await distributedLockService.TryAcquireLockAsync(
            resourceId,
            lockExpiryInSeconds,
            cancellationToken);

        if (!lockAcquired)
        {
            throw new DistributedLockAcquisitionException(resourceId);
        }

        try
        {
            await action();
        }
        finally
        {
            try
            {
                await distributedLockService.ReleaseLockAsync(resourceId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "Failed to release distributed lock for resource {ResourceId}",
                    resourceId);
            }
        }
    }

    /// <summary>
    /// Executes a function within a distributed lock context, automatically handling lock acquisition and release.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="distributedLockService">The distributed lock service instance.</param>
    /// <param name="resourceId">The unique identifier for the resource to lock.</param>
    /// <param name="lockExpiryInSeconds">The lock expiry time in seconds.</param>
    /// <param name="func">The function to execute within the lock context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <param name="logger">Optional logger for lock operation warnings.</param>
    /// <returns>A task representing the asynchronous operation with the function result.</returns>
    /// <exception cref="DistributedLockAcquisitionException">Thrown when the lock cannot be acquired.</exception>
    public static async Task<T> ExecuteWithLockAsync<T>(
        this IDistributedLockService distributedLockService,
        string resourceId,
        int lockExpiryInSeconds,
        Func<Task<T>> func,
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        var lockAcquired = await distributedLockService.TryAcquireLockAsync(
            resourceId,
            lockExpiryInSeconds,
            cancellationToken);

        if (!lockAcquired)
        {
            throw new DistributedLockAcquisitionException(resourceId);
        }

        try
        {
            return await func();
        }
        finally
        {
            try
            {
                await distributedLockService.ReleaseLockAsync(resourceId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "Failed to release distributed lock for resource {ResourceId}",
                    resourceId);
            }
        }
    }
}

/// <summary>
/// Exception thrown when a distributed lock cannot be acquired.
/// </summary>
public class DistributedLockAcquisitionException : UserFriendlyException
{
    /// <summary>
    /// Gets the resource ID that could not be locked.
    /// </summary>
    public string ResourceId { get; }

    /// <summary>
    /// Initializes a new instance of the DistributedLockAcquisitionException class.
    /// </summary>
    /// <param name="resourceId">The resource ID that could not be locked.</param>
    public DistributedLockAcquisitionException(string resourceId)
        : base($"Failed to acquire distributed lock for resource: {resourceId}", WorkflowErrorCodes.Locked)
    {
        ResourceId = resourceId;
    }

    /// <summary>
    /// Initializes a new instance of the DistributedLockAcquisitionException class.
    /// </summary>
    /// <param name="resourceId">The resource ID that could not be locked.</param>
    /// <param name="message">The exception message.</param>
    public DistributedLockAcquisitionException(string resourceId, string message)
        : base(message, WorkflowErrorCodes.Locked)
    {
        ResourceId = resourceId;
    }

    /// <summary>
    /// Initializes a new instance of the DistributedLockAcquisitionException class.
    /// </summary>
    /// <param name="code">The exception code</param>
    /// <param name="resourceId">The resource ID that could not be locked.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DistributedLockAcquisitionException(string resourceId, string message, Exception innerException)
        : base(message, WorkflowErrorCodes.Locked, innerException: innerException)
    {
        ResourceId = resourceId;
    }
}
