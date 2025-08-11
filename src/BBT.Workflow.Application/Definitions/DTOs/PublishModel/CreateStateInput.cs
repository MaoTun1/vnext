using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Definitions;

public sealed class CreateStateInput
{
    /// <summary>
    /// If present, it is the more readable key value of the record.
    /// </summary>
    [Required]
    [StringLength(StateConstants.MaxKeyLength)]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$",
        ErrorMessage = "The Key field can only contain alphanumeric characters (A-Z, a-z, 0-9) and hyphens (-).")]
    public string Key { get; set; }

    /// <summary>
    /// <see cref="StateType"/>
    /// </summary>
    [EnumValueValidation(typeof(StateType))]
    public StateType StateType { get; set; }
    
    [Required]
    [StringLength(TransitionConstants.MaxVersionStrategyLength)]
    [AllowedValues("Minor", "Major")]
    public string VersionStrategy { get; set; }

    /// <summary>
    /// It is a content set with multiple language options for the content to be displayed to the user.
    /// </summary>
    public List<CreateLanguageLabelInput> Labels { get; set; }

    /// <summary>
    /// View
    /// </summary>
    public ReferenceInput? View { get; set; }
    
    /// <summary>
    /// Sub Flow
    /// </summary>
    public SubFlowInput? SubFlow { get; set; }

    /// <summary>
    /// Transitions
    /// </summary>
    public List<CreateTransitionInput>? Transitions { get; set; }

    /// <summary>
    /// On entries tasks
    /// </summary>
    public List<OnExecuteTaskInput>? OnEntries { get; set; }

    /// <summary>
    /// On exits tasks
    /// </summary>
    public List<OnExecuteTaskInput>? OnExits { get; set; }
}