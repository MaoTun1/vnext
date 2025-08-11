using System.ComponentModel.DataAnnotations;

namespace BBT.Workflow.Definitions;

public sealed class CreateLanguageLabelInput
{
    /// <summary>
    /// The text content to be displayed to the user.
    /// </summary>
    [Required]
    [StringLength(LanguageLabelConstants.MaxLabelLength)]
    public string Label { get; set; }
    /// <summary>
    /// The language code of the text in ISO 639 format.
    /// </summary>
    [Required]
    [StringLength(LanguageLabelConstants.MaxLanguageLength)]
    public string Language { get; set; }
}