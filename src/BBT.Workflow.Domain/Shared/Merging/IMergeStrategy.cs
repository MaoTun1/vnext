namespace BBT.Workflow.Shared.Merging;

/// <summary>
/// Defines a strategy for merging two objects of any type.
/// Implementations should handle specific type combinations and merging logic.
/// </summary>
public interface IMergeStrategy
{
    /// <summary>
    /// Merges two objects according to the strategy's specific logic.
    /// </summary>
    /// <param name="target">The target object to merge into.</param>
    /// <param name="source">The source object to merge from.</param>
    /// <returns>The merged result object.</returns>
    object? Merge(object? target, object? source);
}
