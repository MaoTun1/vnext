namespace BBT.Workflow.Caching;

public abstract class CacheContext
{
    protected List<ICacheSet> CacheSets = new();

    public virtual async Task InitializeAsync(Dictionary<Type, object> initialData,
        CancellationToken cancellationToken = default)
    {
        foreach (var cacheSet in CacheSets)
        {
            var cacheSetType = cacheSet.GetType().GetGenericArguments()[0];

            if (initialData.TryGetValue(cacheSetType, out var data))
            {
                await cacheSet.LoadAllAsync(data, cancellationToken);
            }
        }
    }
}