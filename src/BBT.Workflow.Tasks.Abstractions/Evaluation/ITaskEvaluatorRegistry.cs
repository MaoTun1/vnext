using BBT.Aether.Results;

namespace BBT.Workflow.Tasks.Evaluation;

/// <summary>
/// Registry for task evaluators.
/// Provides access to evaluators by type.
/// </summary>
public interface ITaskEvaluatorRegistry
{
    /// <summary>
    /// Gets an evaluator for the specified evaluation type.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="evaluationType">The evaluation type (e.g., "Condition", "Timer").</param>
    /// <returns>A Result containing the evaluator or an error if not found.</returns>
    Result<ITaskEvaluator<TResult>> GetEvaluator<TResult>(string evaluationType);
    
    /// <summary>
    /// Checks if an evaluator is registered for the evaluation type.
    /// </summary>
    /// <param name="evaluationType">The evaluation type to check.</param>
    /// <returns>True if an evaluator is registered.</returns>
    bool HasEvaluator(string evaluationType);
    
    /// <summary>
    /// Gets all supported evaluation types.
    /// </summary>
    /// <returns>Collection of supported evaluation types.</returns>
    IEnumerable<string> GetSupportedTypes();
}

