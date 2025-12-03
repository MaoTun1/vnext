using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks.Execution;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Provides workflow task-specific extension methods for the Result pattern.
/// These extensions enable fluent Railway Oriented Programming for task operations.
/// </summary>
public static class TaskResultExtensions
{
    /// <summary>
    /// Creates an OnExecuteTask configuration from the task execution request input.
    /// </summary>
    /// <param name="result">The source result containing task execution input.</param>
    /// <returns>A Result containing the created OnExecuteTask configuration.</returns>
    public static Result<OnExecuteTask> CreateOnExecuteTask(this Result<TaskExecutionRequestInput> result)
    {
        if (!result.IsSuccess)
            return Result<OnExecuteTask>.Fail(result.Error);

        var input = result.Value!;
        var onExecuteTask = OnExecuteTask.Create(
            input.OnExecuteTask.Order,
            input.OnExecuteTask.Task,
            new ScriptCode(
                input.OnExecuteTask.Mapping.Location ?? "./",
                input.OnExecuteTask.Mapping.Code ?? string.Empty,
                input.OnExecuteTask.Mapping.Type)
        );

        return Result<OnExecuteTask>.Ok(onExecuteTask);
    }

    /// <summary>
    /// Creates a TaskExecutionContext containing all necessary components for task execution.
    /// </summary>
    /// <param name="result">The source result containing task execution input.</param>
    /// <param name="onExecuteTask">The task execution configuration.</param>
    /// <param name="scriptContext">The script execution context.</param>
    /// <returns>A Result containing the task execution context.</returns>
    public static Result<TaskExecutionContext> WithExecutionContext(
        this Result<TaskExecutionRequestInput> result,
        OnExecuteTask onExecuteTask,
        ScriptContext scriptContext)
    {
        if (!result.IsSuccess)
            return Result<TaskExecutionContext>.Fail(result.Error);

        var input = result.Value!;
        var context = new TaskExecutionContext(
            input,
            onExecuteTask,
            scriptContext);

        return Result<TaskExecutionContext>.Ok(context);
    }

    /// <summary>
    /// Ensures the task orchestrator is of the expected type for local execution.
    /// </summary>
    /// <typeparam name="T">The expected orchestrator type.</typeparam>
    /// <param name="orchestrator">The task orchestrator to check.</param>
    /// <param name="error">The error to return if the type check fails.</param>
    /// <returns>A Result containing the casted orchestrator or an error.</returns>
    public static Result<T> EnsureOrchestratorType<T>(this ITaskOrchestrator orchestrator, Error error)
        where T : class, ITaskOrchestrator
    {
        return orchestrator is T typed
            ? Result<T>.Ok(typed)
            : Result<T>.Fail(error);
    }
}

/// <summary>
/// Represents the execution context for a task operation containing all necessary components.
/// </summary>
/// <param name="Input">The original task execution request input.</param>
/// <param name="OnExecuteTask">The task execution configuration.</param>
/// <param name="ScriptContext">The script execution context.</param>
public sealed record TaskExecutionContext(
    TaskExecutionRequestInput Input,
    OnExecuteTask OnExecuteTask,
    ScriptContext ScriptContext);

