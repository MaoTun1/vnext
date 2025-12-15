using System.Text.Json;

namespace BBT.Workflow.Execution.Invokers;

/// <summary>
/// Non-generic task invoker interface for registry operations.
/// Allows dynamic binding deserialization based on task type.
/// </summary>
public interface ITaskInvoker
{
    /// <summary>
    /// Task type this invoker handles (e.g., "http", "daprService").
    /// </summary>
    string TaskType { get; }
    
    /// <summary>
    /// The binding type this invoker expects.
    /// </summary>
    Type BindingType { get; }
    
    /// <summary>
    /// Invokes the task with raw JSON binding (for dynamic deserialization).
    /// </summary>
    /// <param name="taskKey">Task key for logging.</param>
    /// <param name="binding">Raw binding as JsonElement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task invocation result.</returns>
    Task<TaskInvocationResult> InvokeAsync(
        string? taskKey,
        JsonElement binding,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Strongly-typed task invoker interface.
/// Used when binding type is known at compile time.
/// </summary>
/// <typeparam name="TBinding">The binding type for this invoker.</typeparam>
public interface ITaskInvoker<TBinding> : ITaskInvoker where TBinding : class
{
    /// <summary>
    /// Invokes the task with strongly-typed binding.
    /// </summary>
    /// <param name="descriptor">Task descriptor with typed binding.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task invocation result.</returns>
    Task<TaskInvocationResult> InvokeAsync(
        TaskDescriptor<TBinding> descriptor,
        CancellationToken cancellationToken = default);
}
