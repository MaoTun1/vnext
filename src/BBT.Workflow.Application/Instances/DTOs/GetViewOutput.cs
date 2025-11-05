namespace BBT.Workflow.Instances;

/// <summary>
/// Output for retrieving instance view
/// </summary>
public sealed class GetViewOutput
{
    /// <summary>
    /// The view key
    /// </summary>
    public string Key { get; set; }
    
    /// <summary>
    /// The view content as JSON
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// The view type
    /// </summary>
    public string Type { get; set; }
}

