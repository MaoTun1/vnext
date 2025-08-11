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
     public string? SortValue { get; set; } = string.Empty;
}

/// <summary>
/// Paginated result for instance list retrieval
/// </summary>
public sealed class GetInstanceListOutput
{
    public List<GetInstanceOutput> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Output for instance history (all data transitions)
/// </summary>
public sealed class GetInstanceHistoryOutput
{
    public List<GetInstanceOutput> Transitions { get; set; } = [];
} 