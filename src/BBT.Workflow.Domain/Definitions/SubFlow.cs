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
        ScriptCode mapping
    )
    {
        Type = type;
        Process = process;
        Mapping = mapping;
    }

    public SubFlowType Type { get; private set; }
    public Reference Process { get; private set; }
    public ScriptCode Mapping { get; private set; }

    public static SubFlow Create(string type, IReference reference, ScriptCode mapping)
    {
        return new SubFlow(SubFlowType.FromCode(type), reference.ToReference(), mapping);
    }
}