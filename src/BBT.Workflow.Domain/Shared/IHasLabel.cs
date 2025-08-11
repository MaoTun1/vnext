using System.Text.Json.Serialization;
using BBT.Aether;
using BBT.Aether.Domain.Values;
using BBT.Workflow.Definitions;

namespace BBT.Workflow;

public interface IHasLabel
{
    /// <summary>
    /// The text content to be displayed to the user.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The language code of the text in ISO 639 format.
    /// </summary>
    public string Language { get; }
}

/// <summary>
/// It is a content set with multiple language options for the content to be displayed to the user.
/// </summary>
public class LanguageLabel : ValueObject, IHasLabel
{
    private LanguageLabel()
    {
    }

    [JsonConstructor]
    internal LanguageLabel(
        string label,
        string language)
    {
        Label = Check.NotNullOrEmpty(label, nameof(Label), LanguageLabelConstants.MaxLabelLength);
        Language = Check.NotNullOrEmpty(language, nameof(Language), LanguageLabelConstants.MaxLanguageLength);
    }

    /// <summary>
    /// The text content to be displayed to the user.
    /// </summary>
    public string Label { get; private set; }

    /// <summary>
    /// The language code of the text in ISO 639 format.
    /// </summary>
    public string Language { get; private set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Label;
        yield return Language;
    }
}