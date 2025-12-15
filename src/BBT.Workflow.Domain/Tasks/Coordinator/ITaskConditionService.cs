using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.Coordinator;

/// <summary>
/// Service interface for evaluating workflow condition scripts.
/// Uses the Result pattern for Railway-oriented error handling.
/// </summary>
public interface ITaskConditionService
{
    /// <summary>
    /// Executes a condition script and returns the boolean result wrapped in a Result.
    /// </summary>
    /// <param name="script">The script code containing the condition logic to evaluate.</param>
    /// <param name="context">The script execution context for condition evaluation.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>
    /// A Result containing true if the condition is met, false otherwise,
    /// or an error if script execution fails.
    /// </returns>
    Task<Result<bool>> ExecuteConditionAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}
