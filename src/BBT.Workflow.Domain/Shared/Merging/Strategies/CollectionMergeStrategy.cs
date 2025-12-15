using System.Collections;
using System.Dynamic;
using System.Text.Json;

namespace BBT.Workflow.Shared.Merging;

/// <summary>
/// Merge strategy for collection types including dictionaries and enumerable objects.
/// Handles dictionary merging with recursive value merging and collection concatenation.
/// </summary>
public class CollectionMergeStrategy : IMergeStrategy
{
    /// <summary>
    /// Merges two collections by concatenating them or merging their contents.
    /// </summary>
    /// <param name="target">The target collection.</param>
    /// <param name="source">The source collection.</param>
    /// <returns>The merged collection.</returns>
    public object? Merge(object? target, object? source)
    {
        if (target is not IEnumerable targetCollection || source is not IEnumerable sourceCollection)
            return source;

        return MergeCollections(targetCollection, sourceCollection);
    }

    /// <summary>
    /// Merges two collections by concatenating them or merging dictionary contents.
    /// </summary>
    /// <param name="target">The target collection.</param>
    /// <param name="source">The source collection.</param>
    /// <returns>The merged collection.</returns>
    private static object? MergeCollections(IEnumerable target, IEnumerable source)
    {
        // Handle Dictionary<string, object> types
        if (target is IDictionary<string, object?> targetDict && source is IDictionary<string, object?> sourceDict)
        {
            var result = new ExpandoObject() as IDictionary<string, object?>;

            // Add all target properties
            foreach (var kvp in targetDict)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Merge source properties
            foreach (var kvp in sourceDict)
            {
                result[kvp.Key] = result.TryGetValue(kvp.Key, out var value)
                    ? ObjectMerger.MergeValues(value, kvp.Value)
                    : kvp.Value;
            }

            return (ExpandoObject)result;
        }

        // For other enumerable types, perform full deep merge
        try
        {
            var targetList = target.Cast<object>().ToList();
            var sourceList = source.Cast<object>().ToList();

            return PerformFullDeepMerge(targetList, sourceList);
        }
        catch
        {
            // If casting fails, return source
            return source;
        }
    }

    /// <summary>
    /// Performs full deep merge of two object lists without any key-based restrictions.
    /// Merges objects at same positions and appends remaining items.
    /// </summary>
    /// <param name="targetList">The target object list.</param>
    /// <param name="sourceList">The source object list.</param>
    /// <returns>The fully merged object list.</returns>
    private static List<object> PerformFullDeepMerge(List<object> targetList, List<object> sourceList)
    {
        var mergedItems = new List<object>();
        var maxLength = Math.Max(targetList.Count, sourceList.Count);

        // Merge items at same positions
        for (int i = 0; i < maxLength; i++)
        {
            if (i < targetList.Count && i < sourceList.Count)
            {
                // Both lists have items at this position - deep merge them
                var mergedItem = ObjectMerger.MergeValues(targetList[i], sourceList[i]);
                mergedItems.Add(mergedItem ?? sourceList[i]);
            }
            else if (i < targetList.Count)
            {
                // Only target has item at this position
                mergedItems.Add(targetList[i]);
            }
            else
            {
                // Only source has item at this position
                mergedItems.Add(sourceList[i]);
            }
        }

        return mergedItems;
    }
}