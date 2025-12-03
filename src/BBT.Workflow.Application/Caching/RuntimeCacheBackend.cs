using BBT.Aether.Results;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace BBT.Workflow.Caching;

/// <summary>
/// Implementation of ICacheBackend that uses IRuntimeService to load entities from the database.
/// This class provides the bridge between the cache layer and the runtime data access layer.
/// Infrastructure errors (database, connection) are allowed to throw exceptions per Railway Pattern guidelines.
/// </summary>
/// <typeparam name="T">The type of entity to load from the runtime backend</typeparam>
public sealed class RuntimeCacheBackend<T>(
    IServiceScopeFactory scopeFactory)
    : ICacheBackend<T>
    where T : class, IDomainEntity, IReferenceSetter
{
    public async Task<Result<T>> LoadAsync(
        string domain,
        string key,
        string? version,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
        var runtimeInfoProvider = scope.ServiceProvider.GetRequiredService<IRuntimeInfoProvider>();

        runtimeInfoProvider.Check(domain);

        // Infrastructure exceptions (DB, connection) will bubble up - this is expected per Railway Pattern
        if (!string.IsNullOrEmpty(version))
        {
            var entity = await runtimeService.GetAsync<T>(key, version, cancellationToken);
            
            if (entity is null)
            {
                return Result<T>.Fail(CacheErrors.ItemNotFoundInBackend<T>(domain, key, version));
            }
            
            return Result<T>.Ok(entity);
        }

        // Version is null: find latest version among all entities in the flow

        var all = await runtimeService.GetAsync<T>(cancellationToken);
        var filtered = all
            .Where(e => e is not null &&
                        string.Equals(e.Domain, domain, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!filtered.Any())
        {
            return Result<T>.Fail(CacheErrors.ItemNotFoundInBackend<T>(domain, key, null));
        }

        var latest = filtered
            .OrderByDescending(e => e!.Version, new SemVersionComparer())
            .FirstOrDefault();

        if (latest is null)
        {
            return Result<T>.Fail(CacheErrors.ItemNotFoundInBackend<T>(domain, key, null));
        }

        return Result<T>.Ok(latest);
    }
}

