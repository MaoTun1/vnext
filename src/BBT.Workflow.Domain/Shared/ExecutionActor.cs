namespace BBT.Workflow.Shared;

/// <summary>
/// Defines who or what is executing the transition
/// </summary>
public enum ExecutionActor
{
    /// <summary>
    /// Transition executed by end user via API
    /// </summary>
    User = 0,
    
    /// <summary>
    /// Transition executed by system processes (auto transitions, scheduled transitions, etc.)
    /// </summary>
    System = 1
}
