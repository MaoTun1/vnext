using System.Runtime.CompilerServices;

namespace BBT.Workflow.Shared.Merging;

/// <summary>
/// Central object merger that coordinates different merge strategies.
/// Provides a unified entry point for all object merging operations with performance optimizations.
/// </summary>
public static class ObjectMerger
{
    /// <summary>
    /// Merges two objects using the most appropriate strategy based on their types.
    /// Handles null cases, type-specific merging, and recursive deep merging.
    /// Optimized for high-frequency usage with aggressive inlining.
    /// </summary>
    /// <param name="target">The target object to merge into.</param>
    /// <param name="source">The source object to merge from.</param>
    /// <returns>The merged result object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? MergeValues(object? target, object? source)
    {
        // Handle null cases first
        if (source == null) return target;
        if (target == null) return source;

        // Use type-specific merge strategies (singleton instances)
        var strategy = MergeStrategyFactory.GetStrategy(target, source);
        return strategy.Merge(target, source);
    }
}
