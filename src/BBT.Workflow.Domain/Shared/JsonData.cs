using System.Text.Json;
using BBT.Aether.Domain.Values;

namespace BBT.Workflow;

/// <summary>
/// Json Data
/// </summary>
public class JsonData : ValueObject
{
    private const string EmptyJson = "{}";
    public static readonly JsonData Empty = new("{}");
    private JsonData()
    {
    }

    public JsonData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            json = EmptyJson;

        Json = json;
    }

    public JsonData(JsonElement? json)
    {
        Json = json is null ? EmptyJson : JsonSerializer.Serialize(json, JsonSerializerConstants.JsonOptions);
    }

    public string Json { get; private set; } = "{}";
    
    private string? _normalizedJson;
    
    /// <summary>
    /// Gets the normalized JSON string for consistent hashing and comparison
    /// </summary>
    public string NormalizedJson
    {
        get
        {
            if (_normalizedJson == null)
            {
                _normalizedJson = NormalizeJson(Json);
            }
            return _normalizedJson;
        }
    }
    
    public JsonElement JsonElement =>
        JsonSerializer.Deserialize<JsonElement>(Json, JsonSerializerConstants.JsonOptions)!;

    public JsonData Merge(JsonData newData)
    {
        if (JsonElement.ValueKind != JsonValueKind.Object || newData.JsonElement.ValueKind != JsonValueKind.Object)
        {
            // If either is not an object, return the new data
            return newData;
        }

        var mergedDict = new Dictionary<string, JsonElement>();

        // First add all properties from the current object
        foreach (var property in JsonElement.EnumerateObject())
        {
            mergedDict[property.Name] = property.Value;
        }

        // Then merge/override with properties from new data
        foreach (var property in newData.JsonElement.EnumerateObject())
        {
            if (mergedDict.ContainsKey(property.Name) && 
                mergedDict[property.Name].ValueKind == JsonValueKind.Object && 
                property.Value.ValueKind == JsonValueKind.Object)
            {
                // Deep merge nested objects
                var currentNestedData = new JsonData(mergedDict[property.Name]);
                var newNestedData = new JsonData(property.Value);
                var mergedNestedData = currentNestedData.Merge(newNestedData);
                mergedDict[property.Name] = mergedNestedData.JsonElement;
            }
            else
            {
                // Override or add new property
                mergedDict[property.Name] = property.Value;
            }
        }

        return new JsonData(JsonSerializer.Serialize(mergedDict, JsonSerializerConstants.JsonOptions));
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Json;
    }

    /// <summary>
    /// Normalizes JSON string to ensure consistent hashing regardless of formatting
    /// </summary>
    /// <param name="json">The JSON string to normalize</param>
    /// <returns>Normalized JSON string</returns>
    private static string NormalizeJson(string json)
    {
        try
        {
            // Parse JSON to remove formatting differences
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            
            // Normalize the JSON element by sorting properties recursively
            var normalizedElement = NormalizeJsonElement(jsonElement);
            
            // Re-serialize with consistent options for deterministic output
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null, // Keep original property names
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            return JsonSerializer.Serialize(normalizedElement, options);
        }
        catch
        {
            // If JSON parsing fails, return original string
            return json;
        }
    }

    /// <summary>
    /// Recursively normalizes a JsonElement by sorting object properties
    /// </summary>
    /// <param name="element">The JsonElement to normalize</param>
    /// <returns>Normalized JsonElement</returns>
    private static JsonElement NormalizeJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                // Sort properties by name for consistent ordering
                var sortedProperties = element.EnumerateObject()
                    .OrderBy(prop => prop.Name, StringComparer.Ordinal)
                    .ToDictionary(
                        prop => prop.Name,
                        prop => NormalizeJsonElement(prop.Value) // Recursive normalization
                    );
                
                return JsonSerializer.SerializeToElement(sortedProperties);
                
            case JsonValueKind.Array:
                // Normalize each array element
                var normalizedArray = element.EnumerateArray()
                    .Select(NormalizeJsonElement)
                    .ToArray();
                
                return JsonSerializer.SerializeToElement(normalizedArray);
                
            default:
                // For primitive values, return as-is
                return element;
        }
    }

    public static JsonData CreateFrom(string json)
    {
        return new JsonData(json);
    }
}