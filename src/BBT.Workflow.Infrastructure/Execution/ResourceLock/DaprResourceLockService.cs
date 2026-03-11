#pragma warning disable DAPR_DISTRIBUTEDLOCK

using Dapr.Client;
using BBT.Workflow.Execution;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.Execution.ResourceLock;

/// <summary>
/// Dapr-backed implementation of <see cref="IResourceLockService"/>.
/// Uses the Dapr distributed lock building block (lock.redis) for
/// explicit Acquire / Release / Extend semantics.
/// </summary>
public sealed class DaprResourceLockService(
    DaprClient daprClient,
    string lockStoreName,
    ILogger<DaprResourceLockService> logger) : IResourceLockService
{
    /// <inheritdoc />
    public async Task<bool> AcquireAsync(
        string resourceKey, string owner, int ttlSeconds, CancellationToken cancellationToken)
    {
        try
        {
            var response = await daprClient.Lock(
                lockStoreName, resourceKey, owner, ttlSeconds, cancellationToken);

            if (response.Success)
            {
                logger.LogInformation(
                    "Resource lock acquired: key={ResourceKey}, owner={Owner}, ttl={TtlSeconds}s",
                    resourceKey, owner, ttlSeconds);
                return true;
            }

            logger.LogWarning(
                "Resource lock conflict: key={ResourceKey}, owner={Owner}. Resource is already locked.",
                resourceKey, owner);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to acquire resource lock: key={ResourceKey}, owner={Owner}",
                resourceKey, owner);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseAsync(
        string resourceKey, string owner, CancellationToken cancellationToken)
    {
        try
        {
            var response = await daprClient.Unlock(
                lockStoreName, resourceKey, owner, cancellationToken);

            var success = response.status == LockStatus.Success;

            if (success)
            {
                logger.LogInformation(
                    "Resource lock released: key={ResourceKey}, owner={Owner}",
                    resourceKey, owner);
            }
            else
            {
                logger.LogWarning(
                    "Resource lock release failed: key={ResourceKey}, owner={Owner}, status={Status}",
                    resourceKey, owner, response.status);
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to release resource lock: key={ResourceKey}, owner={Owner}",
                resourceKey, owner);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExtendAsync(
        string resourceKey, string owner, int ttlSeconds, CancellationToken cancellationToken)
    {
        // Dapr lock API does not have a native extend; re-acquire with the same owner.
        // Redis-backed lock components allow the same owner to refresh the TTL.
        var response = await daprClient.Lock(
            lockStoreName, resourceKey, owner, ttlSeconds, cancellationToken);

        if (response.Success)
        {
            logger.LogInformation(
                "Resource lock extended: key={ResourceKey}, owner={Owner}, ttl={TtlSeconds}s",
                resourceKey, owner, ttlSeconds);
            return true;
        }

        logger.LogWarning(
            "Resource lock extend failed (not held by owner?): key={ResourceKey}, owner={Owner}",
            resourceKey, owner);
        return false;
    }
}
