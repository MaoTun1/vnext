namespace BBT.Workflow.Tasks;

public class TaskExecutionResponseOutput
{
    public bool Success { get; set; }
    public string Message { get; set; }
    
    /// <summary>
    /// Updated context data after task execution.
    /// This includes changes to TaskResponse, Instance data, and other mutable context properties
    /// that need to be synchronized back to the orchestration service.
    /// </summary>
    public TaskContextUpdateOutput? ContextUpdate { get; set; }
}

/// <summary>
/// Represents the context changes that occurred during task execution.
/// Only includes mutable properties that can change during task execution.
/// </summary>
public class TaskContextUpdateOutput
{
    /// <summary>
    /// Updated task responses after execution
    /// </summary>
    public Dictionary<string, object?> TaskResponse { get; set; } = new();
    
    /// <summary>
    /// Updated instance data (if any new data was added)
    /// </summary>
    public Dictionary<string, TaskInstanceDataUpdatesOutput> InstanceDataUpdates { get; set; } = new();
    
    /// <summary>
    /// Updated metadata (if changed during execution)
    /// </summary>
    public Dictionary<string, object>? MetaData { get; set; }
    
    /// <summary>
    /// Updated body (if modified during execution)
    /// </summary>
    public object? Body { get; set; }
    
    /// <summary>
    /// Flag indicating if the body was modified during execution
    /// </summary>
    public bool BodyModified { get; set; }
}

public class TaskInstanceDataUpdatesOutput
{
    public Guid Id { get; set; }
    public string Data { get; set; }
}