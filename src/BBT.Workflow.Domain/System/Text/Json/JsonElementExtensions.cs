namespace System.Text.Json;

public static class JsonElementExtensions
{
    /// <summary>
    /// Converts a JsonElement (must be an object) to Dictionary&lt;string, string&gt;
    /// </summary>
    public static Dictionary<string, string> ToDictionary(this JsonElement element)
    {
        var dictionary = new Dictionary<string, string>();

        if (element.ValueKind != JsonValueKind.Object)
            return dictionary;

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = property.Value.ToString();
        }

        return dictionary;
    }

    public static JsonElement ToJsonElement(this string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}