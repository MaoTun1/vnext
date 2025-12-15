using System.Dynamic;
using System.Text.Json;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Shared.Merging;

/// <summary>
/// Merge strategy for JsonElement instances and mixed JsonElement/ExpandoObject scenarios.
/// Handles object merging, array concatenation, and type conversions.
/// </summary>
public class JsonElementMergeStrategy : IMergeStrategy
{
    /// <summary>
    /// Merges JsonElement objects and handles mixed JsonElement/ExpandoObject scenarios.
    /// </summary>
    /// <param name="target">The target value (JsonElement or ExpandoObject).</param>
    /// <param name="source">The source value (JsonElement or ExpandoObject).</param>
    /// <returns>The merged result.</returns>
    public object? Merge(object? target, object? source)
    {
        if (target == null || source == null)
            return source ?? target;

        return MergeJsonElements(target, source);
    }

    /// <summary>
    /// Merges JsonElement objects and handles mixed JsonElement/ExpandoObject scenarios.
    /// </summary>
    /// <param name="target">The target value (JsonElement or ExpandoObject).</param>
    /// <param name="source">The source value (JsonElement or ExpandoObject).</param>
    /// <returns>The merged result.</returns>
    private static object? MergeJsonElements(object target, object source)
    {
        // Handle JsonElement to JsonElement merging
        if (target is JsonElement targetElement && source is JsonElement sourceElement)
        {
            // Handle object merging
            if (targetElement.ValueKind == JsonValueKind.Object && sourceElement.ValueKind == JsonValueKind.Object)
            {
                var targetExpandoFromJson = JsonSerializer.Deserialize<ExpandoObject>(targetElement.GetRawText(), ScriptContext.JsonScriptBodyOptions);
                var sourceExpandoFromJson = JsonSerializer.Deserialize<ExpandoObject>(sourceElement.GetRawText(), ScriptContext.JsonScriptBodyOptions);

                if (targetExpandoFromJson != null && sourceExpandoFromJson != null)
                {
                    var expandoStrategy = new ExpandoObjectMergeStrategy();
                    return expandoStrategy.Merge(targetExpandoFromJson, sourceExpandoFromJson);
                }
            }

            // Handle array merging with full deep merge
            if (targetElement.ValueKind == JsonValueKind.Array && sourceElement.ValueKind == JsonValueKind.Array)
            {
                return MergeJsonArraysDeep(targetElement, sourceElement);
            }

            // For other JsonElement types, source takes precedence
            return sourceElement;
        }

        // Handle mixed JsonElement and ExpandoObject scenarios
        if (target is JsonElement targetJson && source is ExpandoObject sourceExp)
        {
            if (targetJson.ValueKind == JsonValueKind.Object)
            {
                var targetExpandoFromMixed = JsonSerializer.Deserialize<ExpandoObject>(targetJson.GetRawText(), ScriptContext.JsonScriptBodyOptions);
                if (targetExpandoFromMixed != null)
                {
                    var expandoStrategy = new ExpandoObjectMergeStrategy();
                    return expandoStrategy.Merge(targetExpandoFromMixed, sourceExp);
                }
            }
            return sourceExp;
        }

        if (target is ExpandoObject targetExp && source is JsonElement sourceJson)
        {
            if (sourceJson.ValueKind == JsonValueKind.Object)
            {
                var sourceExpandoFromMixed = JsonSerializer.Deserialize<ExpandoObject>(sourceJson.GetRawText(), ScriptContext.JsonScriptBodyOptions);
                if (sourceExpandoFromMixed != null)
                {
                    var expandoStrategy = new ExpandoObjectMergeStrategy();
                    return expandoStrategy.Merge(targetExp, sourceExpandoFromMixed);
                }
            }
            return sourceJson;
        }

        // For all other cases, source takes precedence
        return source;
    }

    /// <summary>
    /// Performs full deep merge of two JSON arrays without key-based restrictions.
    /// Merges objects at same positions and appends remaining items.
    /// </summary>
    /// <param name="targetArray">The target JSON array.</param>
    /// <param name="sourceArray">The source JSON array.</param>
    /// <returns>The fully merged JSON array.</returns>
    private static JsonElement MergeJsonArraysDeep(JsonElement targetArray, JsonElement sourceArray)
    {
        var targetList = targetArray.EnumerateArray().ToList();
        var sourceList = sourceArray.EnumerateArray().ToList();
        var mergedItems = new List<JsonElement>();
        var maxLength = Math.Max(targetList.Count, sourceList.Count);

        // Merge items at same positions
        for (int i = 0; i < maxLength; i++)
        {
            if (i < targetList.Count && i < sourceList.Count)
            {
                // Both arrays have items at this position - deep merge them
                var mergedItem = MergeJsonElements(targetList[i], sourceList[i]);
                if (mergedItem is JsonElement jsonElement)
                {
                    mergedItems.Add(jsonElement);
                }
                else
                {
                    // Fallback to source item if merge fails
                    mergedItems.Add(sourceList[i]);
                }
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

        // Convert back to JsonElement
        var mergedJson = JsonSerializer.Serialize(mergedItems.Select(e => e.GetRawText()).ToArray());
        return JsonSerializer.Deserialize<JsonElement>(mergedJson);
    }
}
