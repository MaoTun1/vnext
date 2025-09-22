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

    public static JsonData CreateFrom(string json)
    {
        return new JsonData(json);
    }
}