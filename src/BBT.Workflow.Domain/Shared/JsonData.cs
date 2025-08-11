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
        var mergedDict = new Dictionary<string, JsonElement>();

        if (JsonElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in JsonElement.EnumerateObject())
            {
                mergedDict[property.Name] = property.Value;
            }
        }

        if (newData.JsonElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in newData.JsonElement.EnumerateObject())
            {
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