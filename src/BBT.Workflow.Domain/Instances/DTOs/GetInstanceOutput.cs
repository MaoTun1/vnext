using System.Text.Json;

namespace BBT.Workflow.Instances;

/// <summary>
/// Output for single instance retrieval with extensions
/// </summary>
public sealed class GetInstanceOutput
{
    public Guid? Id { get; set; }
    public string? Key { get; set; } = string.Empty;
    public string? Flow { get; set; } = string.Empty;
    public string? Domain { get; set; } = string.Empty;
    public string? FlowVersion { get; set; } = string.Empty;
    public string? Etag { get; set; } = string.Empty;
    public List<string>? Tags { get; set; } = [];
    public JsonElement? Attributes { get; set; }
    public Dictionary<string, object>? Extensions { get; set; }
}

/// <summary>
/// Output for instance history (all data transitions)
/// </summary>
public sealed class GetInstanceHistoryOutput
{
    public List<GetInstanceOutput> Transitions { get; set; } = [];
}

/// <summary>
/// Output for instance data
/// </summary>
public sealed class GetInstanceDataOutput
{
    public JsonElement? Data { get; set; }
    public string? Etag { get; set; } = string.Empty;
    public Dictionary<string, object>? Extensions { get; set; }
} 
public sealed class GetInstanceDataResponseOutput
{
    public GetInstanceDataResponseOutput(GetInstanceDataOutput output)
    {
        Data = output.Data;
        Extensions = output.Extensions;
    }
    public JsonElement? Data { get; set; }
    public Dictionary<string, object>? Extensions { get; set; }
} 