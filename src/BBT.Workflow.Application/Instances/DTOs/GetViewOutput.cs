using System.Text.Json;

namespace BBT.Workflow.Instances;

/// <summary>
/// Output for retrieving instance view
/// </summary>
public sealed class GetViewOutput
{
    /// <summary>
    /// The view content as JSON
    /// </summary>
    public JsonElement? Content { get; set; }

    /// <summary>
    /// The view type
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The view target
    /// </summary>
    public string? Target { get; set; }
}

