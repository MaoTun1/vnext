namespace BBT.Workflow.Shared.Merging;

/// <summary>
/// Default merge strategy that simply returns the source value.
/// Used as a fallback when no specific merge strategy is applicable.
/// </summary>
public class DefaultMergeStrategy : IMergeStrategy
{
    /// <summary>
    /// Returns the source value, effectively overriding the target.
    /// </summary>
    /// <param name="target">The target object (ignored).</param>
    /// <param name="source">The source object to return.</param>
    /// <returns>The source object.</returns>
    public object? Merge(object? target, object? source)
    {
        // Use source value for non-mergeable types
        return source;
    }
}
