using System.ComponentModel.DataAnnotations;

namespace BBT.Workflow.Shared;

public class ScriptCodeInput
{
    [Required]
    public string Code { get; set; }
    [Required]
    public string Location { get; set; }
}