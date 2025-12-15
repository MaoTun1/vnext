using BBT.Workflow.Definitions;

namespace BBT.Workflow.Shared;

public class ScriptCodeInput
{
    public string? Type { get; set; }
    public string? Code { get; set; }
    public string? Location { get; set; }
    public string? Encoding { get; set; }
    public bool HasValue => !string.IsNullOrWhiteSpace(Code);

    public ScriptCode ToScriptCode()
    {
        return new ScriptCode(
            Location,
            Code,
            MappingType.FromCode(Type),
            CodeEncoding.FromCode(Encoding)
        );
    }
}