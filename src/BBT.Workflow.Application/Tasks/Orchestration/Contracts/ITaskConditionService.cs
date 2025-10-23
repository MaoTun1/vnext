using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks;

public interface ITaskConditionService
{
    /// <summary>
    /// Executes a condition script and returns the boolean result.
    /// </summary>
    /// <param name="script">The script code containing the condition logic to evaluate.</param>
    /// <param name="context">The script execution context for condition evaluation.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains 
    /// true if the condition is met, false otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when script or context is null.</exception>
    Task<bool> ExecuteConditionAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}