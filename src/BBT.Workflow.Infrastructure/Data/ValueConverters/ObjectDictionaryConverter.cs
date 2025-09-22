using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BBT.Workflow.Data.ValueConverters;

internal class ObjectDictionaryConverter() : ValueConverter<ObjectDictionary, string>(
    d => SerializeObject(d),
    s => DeserializeObject(s))
{
    public static readonly JsonSerializerOptions SerializeOptions = new();
    private static string SerializeObject(ObjectDictionary extraProperties)
    {
        var copyDictionary = new Dictionary<string, object?>(extraProperties);
        return JsonSerializer.Serialize(copyDictionary, SerializeOptions);
    }

    public static readonly JsonSerializerOptions DeserializeOptions = new();

    private static ObjectDictionary DeserializeObject(string extraPropertiesAsJson)
    {
        if (extraPropertiesAsJson.IsNullOrEmpty() || extraPropertiesAsJson == "{}")
        {
            return new ObjectDictionary();
        }

        var dictionary = JsonSerializer.Deserialize<ObjectDictionary>(extraPropertiesAsJson, DeserializeOptions) ??
                         new ObjectDictionary();
        
        return dictionary;
    }
}



