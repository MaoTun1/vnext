using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Definitions;

public sealed class CreateTransitionInput
{
    /// <summary>
    /// If present, it is the more readable key value of the record.
    /// </summary>
    [Required]
    [StringLength(TransitionConstants.MaxKeyLength)]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$",
        ErrorMessage = "The Key field can only contain alphanumeric characters (A-Z, a-z, 0-9) and hyphens (-).")]
    public string Key { get; set; }

    /// <summary>
    /// Specifies the targeted state information.
    /// </summary>
    [Required]
    [StringLength(StateConstants.MaxKeyLength)]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$",
        ErrorMessage = "The Target field can only contain alphanumeric characters (A-Z, a-z, 0-9) and hyphens (-).")]
    public string Target { get; set; }

    /// <summary>
    /// Specifies the from state information.
    /// </summary>
    [StringLength(StateConstants.MaxKeyLength)]
    [RegularExpression(@"^[a-zA-Z0-9\-]+$",
        ErrorMessage = "The Target field can only contain alphanumeric characters (A-Z, a-z, 0-9) and hyphens (-).")]
    public string? From { get; set; }

    [Required]
    [StringLength(TransitionConstants.MaxVersionStrategyLength)]
    [AllowedValues("Minor", "Major")]
    public string VersionStrategy { get; set; }

    /// <summary>
    /// <see cref="TriggerType"/>
    /// </summary>
    [EnumValueValidation(typeof(TriggerType))]
    public TriggerType TriggerType { get; set; }

    /// <summary>
    /// Available in
    /// </summary>
    public List<string>? AvailableIn { get; set; } = [];

    /// <summary>
    /// Schema
    /// </summary>
    public ReferenceInput? Schema { get; set; }

    /// <summary>
    /// Rule
    /// </summary>
    public ScriptCodeInput? Rule { get; set; }
    
    /// <summary>
    /// Timer
    /// </summary>
    public ScriptCodeInput? Timer { get; set; }

    /// <summary>
    /// It is a content set with multiple language options for the content to be displayed to the user.
    /// </summary>
    public List<CreateLanguageLabelInput> Labels { get; set; } = [];

    /// <summary>
    /// View
    /// </summary>
    public ReferenceInput? View { get; set; }

    /// <summary>
    /// On execution tasks
    /// </summary>
    public List<OnExecuteTaskInput>? OnExecutionTasks { get; set; } = [];
}