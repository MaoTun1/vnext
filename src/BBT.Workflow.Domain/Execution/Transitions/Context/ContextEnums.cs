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

public enum EpilogueMode
{
    Run,           // Schedule + Auto normal ak
    DispatchOnly,  // sadece enqueue/dispatch, bekleme yok
    Skip           // epilogue adımlarını tamamen atla
}
