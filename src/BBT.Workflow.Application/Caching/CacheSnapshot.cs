namespace BBT.Workflow.Caching;

/// <summary>
/// Immutable snapshot of cache state containing entries and index.
/// This class enables lock-free reads by providing a consistent view of the cache at a point in time.
/// Updates are performed by creating a new snapshot and atomically swapping it with the old one.
/// </summary>
/// <typeparam name="T">The type of entity stored in the cache</typeparam>
public sealed class CacheSnapshot<T>
{
    /// <summary>
    /// Creates a new cache snapshot with the specified entries and index.
    /// </summary>
    /// <param name="entries">The cache entries, keyed by cache key</param>
    /// <param name="index">The version index, keyed by "{Domain}:{Name}"</param>
    public CacheSnapshot(
        IReadOnlyDictionary<string, CacheItem<T>> entries,
        IReadOnlyDictionary<string, SortedSet<string>> index)
    {
        Entries = entries;
        Index = index;
    }

    /// <summary>
    /// Gets the cache entries dictionary mapping cache keys to cache items.
    /// Format: "{EntityType}:{Domain}:{Flow}:{Key}:{Version}" -> CacheItem
    /// </summary>
    public IReadOnlyDictionary<string, CacheItem<T>> Entries { get; }

    /// <summary>
    /// Gets the version index mapping domain:name to a sorted set of versions.
    /// Format: "{Domain}:{Name}" -> SortedSet of versions
    /// </summary>
    public IReadOnlyDictionary<string, SortedSet<string>> Index { get; }
}

