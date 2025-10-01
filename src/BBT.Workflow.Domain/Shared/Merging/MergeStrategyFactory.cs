using System.Collections;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BBT.Workflow.Shared.Merging;

/// <summary>
/// Factory class responsible for selecting the appropriate merge strategy based on object types.
/// Uses singleton instances for optimal performance in high-frequency scenarios.
/// </summary>
public static class MergeStrategyFactory
{
    // Singleton instances for performance optimization - initialized once at startup
    private static readonly IMergeStrategy _expandoObjectStrategy = new ExpandoObjectMergeStrategy();
    private static readonly IMergeStrategy _jsonElementStrategy = new JsonElementMergeStrategy();
    private static readonly IMergeStrategy _collectionStrategy = new CollectionMergeStrategy();
    private static readonly IMergeStrategy _defaultStrategy = new DefaultMergeStrategy();

    /// <summary>
    /// Gets the appropriate merge strategy for the given target and source objects.
    /// Returns singleton instances to avoid memory allocations in high-frequency scenarios.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="source">The source object.</param>
    /// <returns>The most suitable merge strategy for the given object types.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IMergeStrategy GetStrategy(object? target, object? source)
    {
        // Handle null cases
        if (target == null || source == null)
            return _defaultStrategy;

        // Handle ExpandoObject merging (highest priority)
        if (target is ExpandoObject && source is ExpandoObject)
            return _expandoObjectStrategy;
    
        // Handle JsonElement merging
        if (target is JsonElement && source is JsonElement)
            return _jsonElementStrategy;

        // Handle mixed JsonElement and ExpandoObject scenarios
        if ((target is JsonElement && source is ExpandoObject) ||
            (target is ExpandoObject && source is JsonElement))
        {
            return _jsonElementStrategy;
        }
    
        // Handle collections (but not strings, as they implement IEnumerable)
        if (target is IEnumerable && source is IEnumerable && 
            target is not string && source is not string)
            return _collectionStrategy;
        
        return _defaultStrategy;
    }
}
