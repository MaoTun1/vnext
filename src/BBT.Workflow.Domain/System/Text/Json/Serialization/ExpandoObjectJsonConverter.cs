using System.Dynamic;

namespace System.Text.Json.Serialization;

public class ExpandoObjectJsonConverter : JsonConverter<ExpandoObject>
{
    public override ExpandoObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var expandoObject = new ExpandoObject();
        var dictionary = expandoObject as IDictionary<string, object>;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return expandoObject;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var propertyName = reader.GetString();
            reader.Read();

            if (propertyName != null) dictionary[propertyName] = ReadValue(ref reader);
        }

        throw new JsonException();
    }

    private object? ReadValue(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt32(out var intValue) ? intValue : reader.GetDouble(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => Read(ref reader, typeof(ExpandoObject), null),
            JsonTokenType.StartArray => ReadArray(ref reader),
            _ => throw new JsonException($"Unsupported token type: {reader.TokenType}")
        };
    }

    private object[] ReadArray(ref Utf8JsonReader reader)
    {
        var list = new List<object>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(ReadValue(ref reader));
        }
        return list.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, ExpandoObject value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value as IDictionary<string, object>, options);
    }
}