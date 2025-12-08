using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Timer;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.Evaluation;

/// <summary>
/// Unified evaluator interface for special tasks that return typed results.
/// Evaluators are lightweight - no persistence, no metrics, just script evaluation.
/// 
/// Examples: Condition evaluation (bool), Timer evaluation (TimerSchedule)
/// </summary>
/// <typeparam name="TResult">The type of the evaluation result.</typeparam>
public interface ITaskEvaluator<TResult>
{
    /// <summary>
    /// Evaluation type identifier (e.g., "Condition", "Timer").
    /// </summary>
    string EvaluationType { get; }
    
    /// <summary>
    /// Evaluates the script and returns the typed result.
    /// </summary>
    /// <param name="script">The script code to evaluate.</param>
    /// <param name="context">The script context with workflow state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the evaluation result or an error.</returns>
    Task<Result<TResult>> EvaluateAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Condition evaluator interface.
/// Evaluates condition scripts that return boolean results.
/// </summary>
public interface IConditionEvaluator : ITaskEvaluator<bool>
{
}

/// <summary>
/// Timer evaluator interface.
/// Evaluates timer scripts that return TimerSchedule results.
/// </summary>
public interface ITimerEvaluator : ITaskEvaluator<TimerSchedule>
{
}

