using System.Reflection;

namespace System.Text.Json.Serialization;

public sealed class IEquatableJsonConverter<T> : JsonConverter<T> 
    where T : class, IEquatable<T>
{
    private static readonly MethodInfo? FromCodeMethod = typeof(T)
        .GetMethod("FromCode", BindingFlags.Public | BindingFlags.Static);

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (FromCodeMethod == null)
        {
            throw new JsonException($"{typeof(T).Name} must have a static FromCode(string) method.");
        }
        
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a JSON string for {typeof(T).Name}.");
        }

        var code = reader.GetString();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new JsonException($"Invalid or empty code for {typeof(T).Name}.");
        }
        
        return FromCodeMethod.Invoke(null, new object[] { code }) as T;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var codeProperty = typeof(T).GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
        if (codeProperty == null)
        {
            throw new JsonException($"{typeof(T).Name} must have a public 'Code' property.");
        }

        var code = codeProperty.GetValue(value)?.ToString();
        if (code == null)
        {
            throw new JsonException($"Could not read 'Code' property of {typeof(T).Name}.");
        }
        
        writer.WriteStringValue(code);
    }
}