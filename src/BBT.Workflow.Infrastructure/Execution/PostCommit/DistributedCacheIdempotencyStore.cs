using BBT.Aether.DistributedCache;
using BBT.Aether.Results;
using BBT.Workflow.Execution.PostCommit;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.Execution.PostCommit;

/// <summary>
/// Distributed cache implementation of post-commit idempotency store.
/// Uses IDistributedCacheService for tracking job execution across instances.
/// </summary>
public sealed class DistributedCacheIdempotencyStore(
    IDistributedCacheService cache,
    ILogger<DistributedCacheIdempotencyStore> logger) : IPostCommitIdempotencyStore
{
    private const string KeyPrefix = "postcommit:idempotency:";
    private const string StatusPending = "pending";
    private const string StatusCompleted = "completed";
    private const string StatusFailed = "failed";

    /// <summary>
    /// Default TTL for idempotency entries.
    /// Jobs older than this are considered safe to retry.
    /// </summary>
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    /// <inheritdoc />
    public async Task<Result<bool>> TryBeginAsync(string key, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(key);

        try
        {
            // Check if already processed
            var existing = await cache.GetAsync<string>(cacheKey, cancellationToken);
            if (existing is not null)
            {
                logger.LogDebug(
                    "Idempotency key {Key} already exists with status {Status}, skipping",
                    key,
                    existing);
                return Result<bool>.Ok(false);
            }

            // Try to set as pending (first executor wins)
            await cache.SetAsync(
                cacheKey,
                StatusPending,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.Add(DefaultTtl)
                },
                cancellationToken);

            logger.LogDebug("Idempotency key {Key} registered as pending", key);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check/set idempotency key {Key}", key);
            return Result<bool>.Fail(Error.Failure(
                "IDEMPOTENCY_STORE_ERROR",
                $"Failed to access idempotency store: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public async Task MarkCompletedAsync(string key, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(key);

        try
        {
            await cache.SetAsync(
                cacheKey,
                StatusCompleted,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.Add(DefaultTtl)
                },
                cancellationToken);

            logger.LogDebug("Idempotency key {Key} marked as completed", key);
        }
        catch (Exception ex)
        {
            // Log but don't fail - job already completed successfully
            logger.LogWarning(ex, "Failed to mark idempotency key {Key} as completed", key);
        }
    }

    /// <inheritdoc />
    public async Task MarkFailedAsync(string key, string errorCode, string? errorMessage, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(key);

        try
        {
            var value = $"{StatusFailed}:{errorCode}:{errorMessage ?? ""}";
            await cache.SetAsync(
                cacheKey,
                value,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.Add(DefaultTtl)
                },
                cancellationToken);

            logger.LogDebug("Idempotency key {Key} marked as failed: {ErrorCode}", key, errorCode);
        }
        catch (Exception ex)
        {
            // Log but don't fail - error already handled
            logger.LogWarning(ex, "Failed to mark idempotency key {Key} as failed", key);
        }
    }

    private static string GetCacheKey(string key) => $"{KeyPrefix}{key}";
}

