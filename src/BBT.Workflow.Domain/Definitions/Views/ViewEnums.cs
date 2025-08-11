namespace BBT.Workflow.Definitions;

/// <summary>
/// View types
/// </summary>
public enum ViewType
{
    /// <summary>
    /// Json
    /// </summary>
    Json = 1,

    /// <summary>
    /// Html
    /// </summary>
    Html = 2,

    /// <summary>
    /// Markdown
    /// </summary>
    Markdown = 3
}

/// <summary>
/// View targets
/// </summary>
public enum ViewTarget
{
    /// <summary>
    /// State
    /// </summary>
    State = 1,

    /// <summary>
    /// Transition
    /// </summary>
    Transition = 2,

    /// <summary>
    /// Task
    /// </summary>
    Task = 3
}