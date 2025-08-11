using System.Dynamic;

namespace System.Text.Json;

public static class JsonDocumentExtensions
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public static dynamic? ToDynamic(this JsonElement document)
    {
        return ConvertToDynamic(document);
    }
    
    private static dynamic? ConvertToDynamic(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var expando = new ExpandoObject() as IDictionary<string, object?>;
            foreach (var property in element.EnumerateObject())
            {
                expando[property.Name] = ConvertToDynamic(property.Value);
            }
            return expando;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<object?>();
            foreach (var item in element.EnumerateArray())
            {
                list.Add(ConvertToDynamic(item));
            }
            return list;
        }
        else if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;
        else if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt64(out var l)) return l;
            if (element.TryGetDouble(out var d)) return d;
        }
        else if (element.ValueKind == JsonValueKind.True) return true;
        else if (element.ValueKind == JsonValueKind.False) return false;
        else if (element.ValueKind == JsonValueKind.Null) return null;

        return element.ToString() ?? string.Empty;
    }
}