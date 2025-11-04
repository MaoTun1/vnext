using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;
public sealed class SubFlow
{
    private SubFlow()
    {
    }

    [JsonConstructor]
    private SubFlow(
        SubFlowType type,
        Reference process,
        ScriptCode mapping,
        Dictionary<string, Reference>? viewOverrides)
    {
        Type = type;
        Process = process;
        Mapping = mapping;
        ViewOverrides = viewOverrides;
    }

    public SubFlowType Type { get; private set; }
    public Reference Process { get; private set; }
    public ScriptCode Mapping { get; private set; }
    public Dictionary<string, Reference>? ViewOverrides { get; private set; }
    
    [NotMapped]
    [JsonIgnore]
    public bool HasViewOverrides => this.ViewOverrides != null;

    public static SubFlow Create(string type, IReference reference, ScriptCode mapping, Dictionary<string, Reference>? viewOverrides)
    {
        return new SubFlow(SubFlowType.FromCode(type), reference.ToReference(), mapping, viewOverrides);
    }
}