namespace BBT.Workflow.Execution;

/// <summary>
/// Generic container for task invocation with strongly-typed binding.
/// Used when the binding type is known at compile time.
/// </summary>
/// <typeparam name="TBinding">The type of task binding configuration.</typeparam>
public sealed class TaskDescriptor<TBinding> where TBinding : class
{
    /// <summary>
    /// Task type discriminator for invoker resolution (e.g., "http", "daprService").
    /// </summary>
    public required string TaskType { get; init; }
    
    /// <summary>
    /// Version of the binding schema.
    /// </summary>
    public string Version { get; init; } = "1.0.0";
    
    /// <summary>
    /// Task key for logging and tracing.
    /// </summary>
    public string? TaskKey { get; init; }
    
    /// <summary>
    /// Strongly-typed binding configuration.
    /// </summary>
    public required TBinding Binding { get; init; }
}

