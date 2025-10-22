namespace BBT.Workflow.Execution.ReEntry;

/// <summary>
/// Configuration options for re-entry transition execution.
/// </summary>
public sealed class ReentryOptions
{
    /// <summary>
    /// Maximum number of automatic transition hops allowed in a single execution chain.
    /// Prevents infinite loops in automatic transition chains.
    /// Default value is 12.
    /// </summary>
    public int MaxAutoHops { get; set; } = 12;
    
    /// <summary>
    /// Whether to allow inline execution of automatic transitions.
    /// When true, automatic transitions may be executed immediately in the same scope.
    /// When false, all automatic transitions are enqueued as background jobs.
    /// Default value is true.
    /// </summary>
    public bool AllowInlineAuto { get; set; } = true;
    
    /// <summary>
    /// Timeout for distributed locks used during re-entry execution.
    /// Default value is 30 seconds.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
