using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BBT.Workflow.Instances.DTOs;

public class FunctionListQueryParameters
{

    [JsonPropertyName("filter")]
    public string[]? Filter { get; set; } =[];

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 10;
    [JsonPropertyName("sort")]
    public string? Sort { get; set; } = string.Empty;
    [JsonPropertyName("pageUrl")]
    [JsonIgnore]
    public string? PageUrl { get; set; } 
}
