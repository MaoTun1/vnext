using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Shared;

public class OnExecuteTaskInput
{
    [Required] [Range(1, int.MaxValue)] public int Order { get; set; }
    [Required] public ReferenceInput Task { get; set; }
    [Required] public ScriptCodeInput Mapping { get; set; }
}