using System.Text.Json;

namespace BBT.Workflow;

public static class JsonSerializerConstants
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true
    };
}