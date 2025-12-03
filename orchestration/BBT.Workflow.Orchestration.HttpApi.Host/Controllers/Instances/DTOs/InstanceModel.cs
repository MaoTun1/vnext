

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Instances;
public class CreateInstanceDto
{
    [Required]
    [StringLength(InstanceConstants.MaxKeyLength)]
    public string Key { get; set; }
    public string[]? Tags { get; set; }
    public JsonElement? Attributes { get; set; }
}

public class CreateSubInstanceDto : CreateInstanceDto
{
    public Guid? Id  { get; set; }
    public string? Callback { get; set; }
    public Dictionary<string, object?> ExtraProperties { get; set; }
}