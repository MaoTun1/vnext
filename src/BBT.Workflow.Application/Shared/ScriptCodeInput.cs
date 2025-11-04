using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Shared;

public class ScriptCodeInput
{

    public MappingType? Type { get; set; }
    public string? Code { get; set; }
    public string? Location { get; set; }
}