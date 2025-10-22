namespace BBT.Workflow.Execution;

/// <summary>
/// Execution mode enumeration.
/// </summary>
public enum ExecMode
{
    /// <summary>Synchronous execution.</summary>
    Sync = 0,
    
    /// <summary>Asynchronous execution via background jobs.</summary>
    Async = 1
}

/// <summary>
/// Epilogue execution mode for transition pipeline.
/// Controls how automatic and scheduled transitions are handled after state changes.
/// </summary>
public enum EpilogueMode
{
    /// <summary>
    /// Execute schedule and automatic transitions normally.
    /// </summary>
    Run,
    
    /// <summary>
    /// Dispatch transitions without waiting for completion.
    /// </summary>
    DispatchOnly,
    
    /// <summary>
    /// Skip all epilogue steps (schedule and automatic transitions).
    /// </summary>
    Skip
}
