using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;

namespace BBT.Workflow.Scripting;

public sealed class ScriptResponse
{
    public dynamic? Data { get; set; }
    public dynamic? Headers { get; set; }
}

/// <summary>
/// Standardized task execution response that provides consistent structure for all task types.
/// This model includes execution status, data, metadata, and error information.
/// </summary>
public sealed class StandardTaskResponse
{
    /// <summary>
    /// The actual response data from the task execution.
    /// </summary>
    public dynamic? Data { get; set; }

    /// <summary>
    /// HTTP status code for HTTP-based tasks (HttpTask, DaprServiceTask).
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Indicates whether the task execution was successful.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Error message if task execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Response headers for HTTP-based tasks.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Additional metadata about the task execution.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Task execution duration in milliseconds.
    /// </summary>
    public long? ExecutionDurationMs { get; set; }

    /// <summary>
    /// Task type identifier.
    /// </summary>
    public string? TaskType { get; set; }
}

public sealed class ScriptContext
{
    public static readonly JsonSerializerOptions JsonScriptBodyOptions = new()
    {
        Converters = { new ExpandoObjectJsonConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public dynamic? Body { get; private set; }
    public dynamic? Headers { get; private set; }
    public dynamic? RouteValues { get; private set; }
    public Instance Instance { get; private set; }
    public Definitions.Workflow Workflow { get; private set; }
    public IRuntimeInfoProvider Runtime { get; private set; }
    public Transition Transition { get; private set; }
    public Dictionary<string, dynamic> Definitions { get; private set; }
    public Dictionary<string, dynamic?> TaskResponse { get; private set; } = new();
    public Dictionary<string, dynamic> MetaData { get; private set; } = new();

    /// <summary>
    /// Sets the body of the script context. This method is thread-safe and can be used
    /// for context synchronization in distributed scenarios.
    /// </summary>
    /// <param name="body">The new body content.</param>
    public void SetBody(object? body)
    {
        MergeToBody(body, JsonSerializerConstants.JsonOptions);
    }

    /// <summary>
    /// Sets the standardized response body for the script context.
    /// </summary>
    /// <param name="response">The standardized task response.</param>
    public void SetStandardResponse(StandardTaskResponse response)
    {
        MergeToBody(response, JsonScriptBodyOptions);
    }

    /// <summary>
    /// Merges the provided object into the existing Body using the specified JSON options.
    /// If Body is null, it initializes it with the new content.
    /// </summary>
    /// <param name="content">The content to merge into Body.</param>
    /// <param name="jsonOptions">The JSON serialization options to use.</param>
    private void MergeToBody(object? content, JsonSerializerOptions jsonOptions)
    {
        if (content == null)
        {
            return;
        }

        var serializedContent = JsonSerializer.Serialize(content, jsonOptions);
        var newExpando = JsonSerializer.Deserialize<ExpandoObject>(serializedContent, JsonScriptBodyOptions);

        if (newExpando == null)
        {
            return;
        }

        if (Body == null)
        {
            Body = newExpando;
        }
        else
        {
            Body = MergeExpandoObjects(Body, newExpando);
        }
    }

    /// <summary>
    /// Merges two ExpandoObject instances, with properties from the source taking precedence.
    /// Handles all nested structures including JsonElement objects and arrays.
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
            if (targetDict.ContainsKey(kvp.Key))
            {
                var mergedValue = MergeValues(targetDict[kvp.Key], kvp.Value);
                targetDict[kvp.Key] = mergedValue;
            }
            else
            {
                // Add new property
                targetDict[kvp.Key] = kvp.Value;
            }
        }

        return target;
    }

    /// <summary>
    /// Recursively merges two values of any type, handling ExpandoObjects, JsonElements, arrays, and other complex types.
    /// </summary>
    /// <param name="targetValue">The target value to merge into.</param>
    /// <param name="sourceValue">The source value to merge from.</param>
    /// <returns>The merged value.</returns>
    private static object? MergeValues(object? targetValue, object? sourceValue)
    {
        // If source is null, keep target
        if (sourceValue == null)
        {
            return targetValue;
        }

        // If target is null, use source
        if (targetValue == null)
        {
            return sourceValue;
        }

        // Both are ExpandoObjects - merge recursively
        if (targetValue is ExpandoObject targetExpando && sourceValue is ExpandoObject sourceExpando)
        {
            return MergeExpandoObjects(targetExpando, sourceExpando);
        }

        // Handle JsonElement objects by converting them to ExpandoObjects first
        if (targetValue is JsonElement targetJsonElement && sourceValue is JsonElement sourceJsonElement)
        {
            if (targetJsonElement.ValueKind == JsonValueKind.Object && sourceJsonElement.ValueKind == JsonValueKind.Object)
            {
                var targetExpandoFromJson = JsonSerializer.Deserialize<ExpandoObject>(targetJsonElement.GetRawText(), JsonScriptBodyOptions);
                var sourceExpandoFromJson = JsonSerializer.Deserialize<ExpandoObject>(sourceJsonElement.GetRawText(), JsonScriptBodyOptions);
                
                if (targetExpandoFromJson != null && sourceExpandoFromJson != null)
                {
                    return MergeExpandoObjects(targetExpandoFromJson, sourceExpandoFromJson);
                }
            }
        }

        // Handle mixed JsonElement and ExpandoObject
        if (targetValue is JsonElement targetJson && sourceValue is ExpandoObject sourceExp)
        {
            if (targetJson.ValueKind == JsonValueKind.Object)
            {
                var targetExpandoFromJson = JsonSerializer.Deserialize<ExpandoObject>(targetJson.GetRawText(), JsonScriptBodyOptions);
                if (targetExpandoFromJson != null)
                {
                    return MergeExpandoObjects(targetExpandoFromJson, sourceExp);
                }
            }
        }

        if (targetValue is ExpandoObject targetExp && sourceValue is JsonElement sourceJson)
        {
            if (sourceJson.ValueKind == JsonValueKind.Object)
            {
                var sourceExpandoFromJson = JsonSerializer.Deserialize<ExpandoObject>(sourceJson.GetRawText(), JsonScriptBodyOptions);
                if (sourceExpandoFromJson != null)
                {
                    return MergeExpandoObjects(targetExp, sourceExpandoFromJson);
                }
            }
        }

        // Handle arrays - concatenate or merge based on content
        if (targetValue is JsonElement targetArray && sourceValue is JsonElement sourceArray)
        {
            if (targetArray.ValueKind == JsonValueKind.Array && sourceArray.ValueKind == JsonValueKind.Array)
            {
                var targetList = targetArray.EnumerateArray().ToList();
                var sourceList = sourceArray.EnumerateArray().ToList();
                
                // For arrays, we append source items to target
                var mergedArray = new List<JsonElement>();
                mergedArray.AddRange(targetList);
                mergedArray.AddRange(sourceList);
                
                // Convert back to JsonElement
                var mergedJson = JsonSerializer.Serialize(mergedArray.Select(e => e.GetRawText()).ToArray());
                return JsonSerializer.Deserialize<JsonElement>(mergedJson);
            }
        }

        // Handle Dictionary<string, object> types that might come from different serialization contexts
        if (targetValue is IDictionary<string, object?> targetDict && sourceValue is IDictionary<string, object?> sourceDict)
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
                if (result.ContainsKey(kvp.Key))
                {
                    result[kvp.Key] = MergeValues(result[kvp.Key], kvp.Value);
                }
                else
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            return (ExpandoObject)result;
        }

        // For all other types, source takes precedence
        return sourceValue;
    }

    private ScriptContext()
    {
    }

    public sealed class Builder
    {
        private readonly ScriptContext _context = new();

        public Builder SetBody(object? body)
        {
            _context.SetBody(body);
            return this;
        }

        public Builder SetHeaders(object? headers)
        {
            _context.Headers = headers;
            return this;
        }

        public Builder SetRouteValues(object? routeValues)
        {
            _context.RouteValues = routeValues;
            return this;
        }

        public Builder SetWorkflow(Definitions.Workflow workflow)
        {
            _context.Workflow = workflow;
            return this;
        }

        public Builder SetInstance(Instance instance)
        {
            _context.Instance = instance;
            return this;
        }

        public Builder SetTransition(Transition? transition)
        {
            if (transition != null)
            {
                _context.Transition = transition;
            }

            return this;
        }

        public Builder SetRuntime(IRuntimeInfoProvider runtime)
        {
            _context.Runtime = runtime;
            return this;
        }

        public Builder SetDefinitions(Dictionary<string, object> definitions)
        {
            _context.Definitions = definitions;
            return this;
        }

        public Builder SetTaskResponse(Dictionary<string, object?> taskResponse)
        {
            _context.TaskResponse = taskResponse;
            return this;
        }

        public Builder SetMetadata(Dictionary<string, object> metadata)
        {
            _context.MetaData = metadata;
            return this;
        }

        public ScriptContext Build()
        {
            return _context;
        }
    }
}