using System.Text.Json.Serialization;

namespace BBT.Workflow.Instances.DTOs;

public class FunctionQueryParemeters
{
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string? Version { get; set; } = null;
    
    [JsonPropertyName("extension")]
    public string[]? Extension { get; set; } = null;
}
