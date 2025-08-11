using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Definitions;

public class SubFlowInput
{
    [Required]
    [StringLength(WorkflowConstants.MaxTypeLength)]
    [AllowedValues("C", "F", "S", "P",
        ErrorMessage = "The value must be one of the following: C (Core), F (Flow), S (SubFlow), P (SubProcess).")]
    public string Type { get; set; }

    [Required] public ReferenceInput Process { get; set; }
    [Required] public ScriptCodeInput Mapping { get; set; }
}