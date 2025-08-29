namespace BBT.Workflow.Caching;

/// <summary>
/// Wrapper class to track cache item access time and usage
/// </summary>
public class CacheItem<T>(T value)
{
    public T Value { get; } = value;
    public DateTime LastAccessTime { get; private set; } = DateTime.UtcNow;
    public DateTime CreatedTime { get; } = DateTime.UtcNow;
    public int AccessCount { get; private set; } = 1;

    public void UpdateAccess()
    {
        LastAccessTime = DateTime.UtcNow;
        AccessCount++;
    }
}