namespace BBT.Workflow.Tasks;

/// <summary>
/// Execution mode for task invokers.
/// Determines where and how the task is executed.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Executes locally in the current process (Orchestration).
    /// Used for script tasks, triggers, subprocesses, etc.
    /// </summary>
    Local,
    
    /// <summary>
    /// Executes remotely via service invocation (Execution App).
    /// Used for HTTP, Dapr, and other external service calls.
    /// </summary>
    Remote,
    
    /// <summary>
    /// Custom execution strategy defined by the user.
    /// Allows for plugin-style extensibility.
    /// </summary>
    Custom
}

