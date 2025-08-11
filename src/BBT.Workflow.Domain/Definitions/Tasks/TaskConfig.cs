using System.Text.Json;
using BBT.Aether.Domain.Values;

namespace BBT.Workflow.Definitions;

public class TaskConfig : ValueObject
{
    public static readonly TaskConfig Empty = new TaskConfig("{}");

    private TaskConfig()
    {
    }

    public TaskConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            json = Empty.ToString();

        JsonElement = JsonSerializer.Deserialize<JsonElement>(json!, JsonSerializerConstants.JsonOptions);
    }
    
    public TaskConfig(JsonElement json)
    {
        JsonElement = json;
    }

    public JsonElement JsonElement { get; private set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return JsonSerializer.Serialize(JsonElement, JsonSerializerConstants.JsonOptions);
    }
}