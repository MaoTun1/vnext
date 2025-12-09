using BBT.Aether.Results;
using BBT.Workflow.Tasks.Evaluation;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.Evaluators;

/// <summary>
/// Registry implementation for task evaluators.
/// Provides access to evaluators by type.
/// </summary>
public sealed class TaskEvaluatorRegistry : ITaskEvaluatorRegistry
{
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly ITimerEvaluator _timerEvaluator;
    private readonly ILogger<TaskEvaluatorRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of TaskEvaluatorRegistry.
    /// </summary>
    public TaskEvaluatorRegistry(
        IConditionEvaluator conditionEvaluator,
        ITimerEvaluator timerEvaluator,
        ILogger<TaskEvaluatorRegistry> logger)
    {
        _conditionEvaluator = conditionEvaluator;
        _timerEvaluator = timerEvaluator;
        _logger = logger;

        _logger.LogInformation("TaskEvaluatorRegistry initialized with evaluators: {Types}",
            string.Join(", ", GetSupportedTypes()));
    }

    /// <inheritdoc />
    public Result<ITaskEvaluator<TResult>> GetEvaluator<TResult>(string evaluationType)
    {
        object? evaluator = evaluationType.ToLowerInvariant() switch
        {
            "condition" => _conditionEvaluator,
            "timer" => _timerEvaluator,
            _ => null
        };

        if (evaluator is ITaskEvaluator<TResult> typedEvaluator)
        {
            return Result<ITaskEvaluator<TResult>>.Ok(typedEvaluator);
        }

        _logger.LogError("No evaluator registered for type: {EvaluationType}", evaluationType);
        return Result<ITaskEvaluator<TResult>>.Fail(Error.NotFound(
            WorkflowErrorCodes.TaskHandlerNotFound,
            $"No evaluator registered for type: {evaluationType}"));
    }

    /// <inheritdoc />
    public bool HasEvaluator(string evaluationType)
    {
        return evaluationType.ToLowerInvariant() switch
        {
            "condition" => true,
            "timer" => true,
            _ => false
        };
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSupportedTypes()
    {
        yield return "Condition";
        yield return "Timer";
    }
}