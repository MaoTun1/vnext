namespace System.Text.Json.Serialization;

public class StringToEnumJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? rawValue = reader.GetString();
        if (int.TryParse(rawValue, out int intValue))
        {
            return (TEnum)(object)intValue;
        }

        throw new JsonException($"Cannot convert {rawValue} to enum {typeof(TEnum).Name}");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        int intValue = Convert.ToInt32(value);
        writer.WriteStringValue(intValue.ToString());
    }
}