using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Shared;

public abstract class ReferenceInputBase : IReference
{
    /// <summary>
    /// If present, it is the more readable key value of the record.
    /// </summary>
    [Required]
    [StringLength(WorkflowConstants.MaxKeyLength)]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$",
        ErrorMessage = "The Key field can only contain alphanumeric characters (A-Z, a-z, 0-9) and hyphens (-).")]
    public string Key { get; set; }

    /// <summary>
    /// This is information about the domain on which the stream where the record is located.
    /// </summary>
    [Required]
    [StringLength(WorkflowConstants.MaxDomainLength)]
    [RegularExpression(@"^[a-zA-Z\-]+$",
        ErrorMessage = "The Domain field can only contain alphabetic characters (A-Z) and hyphens (-).")]
    public string Domain { get; set; }

    /// <summary>
    /// It is the information on which stream the record is located.
    /// </summary>
    [Required]
    [StringLength(WorkflowConstants.MaxFlowLength)]
    [RegularExpression(@"^[a-z\-]+$",
        ErrorMessage = "The Flow field can only contain lowercase alphabetic characters (a-z) and hyphens (-).")]
    public string Flow { get; set; }

    /// <summary>
    /// This is the version information at the time the record is assigned.
    /// </summary>
    [Required]
    [StringLength(WorkflowConstants.MaxVersionLength)]
    [RegularExpression(@"^\d+\.\d+\.\d+$",
        ErrorMessage = "The Version field must be in the format 'MAJOR.MINOR.PATCH', e.g., '1.0.0'.")]
    public string Version { get; set; }
}

public sealed class ReferenceInput : ReferenceInputBase
{
}