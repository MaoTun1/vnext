using System.Dynamic;

namespace BBT.Workflow.Shared.Merging;

/// <summary>
/// Merge strategy for ExpandoObject instances.
/// Performs deep recursive merging of ExpandoObject properties.
/// </summary>
public class ExpandoObjectMergeStrategy : IMergeStrategy
{
    /// <summary>
    /// Merges two ExpandoObject instances recursively.
    /// </summary>
    /// <param name="target">The target ExpandoObject.</param>
    /// <param name="source">The source ExpandoObject.</param>
    /// <returns>The merged ExpandoObject.</returns>
    public object? Merge(object? target, object? source)
    {
        if (target is not ExpandoObject targetExpando || source is not ExpandoObject sourceExpando)
            return source;

        return MergeExpandoObjects(targetExpando, sourceExpando);
    }

    /// <summary>
    /// Merges two ExpandoObject instances, with properties from the source taking precedence.
    /// Handles all nested structures recursively.
    /// </summary>
    /// <param name="target">The target ExpandoObject to merge into.</param>
    /// <param name="source">The source ExpandoObject to merge from.</param>
    /// <returns>The merged ExpandoObject.</returns>
    private static ExpandoObject MergeExpandoObjects(ExpandoObject target, ExpandoObject source)
    {
        var targetDict = (IDictionary<string, object?>)target;
        var sourceDict = (IDictionary<string, object?>)source;

        foreach (var kvp in sourceDict)
        {
            targetDict[kvp.Key] =
                targetDict.TryGetValue(kvp.Key, out var existingValue)
                    ? ObjectMerger.MergeValues(existingValue, kvp.Value)
                    : kvp.Value;
        }

        return target;
    }
}