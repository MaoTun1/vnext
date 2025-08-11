namespace BBT.Workflow.Definitions;

/// <summary>
/// Defines a contract for creating deep copies of workflow tasks.
/// This interface ensures that tasks can be safely cloned without affecting cached instances.
/// </summary>
public interface ITaskClonable<out T> where T : WorkflowTask
{
    /// <summary>
    /// Creates a deep copy of the current task instance.
    /// </summary>
    /// <returns>A new instance of the task with identical configuration but separate state.</returns>
    T Clone();
}

/// <summary>
/// Non-generic interface for runtime cloning operations.
/// </summary>
public interface ITaskClonable
{
    /// <summary>
    /// Creates a deep copy of the current task instance.
    /// </summary>
    /// <returns>A new instance of the task with identical configuration but separate state.</returns>
    WorkflowTask Clone();
} 