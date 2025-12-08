using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Timer;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.Coordinator;

/// <summary>
/// Service interface for evaluating workflow timer scripts.
/// Uses the Result pattern for Railway-oriented error handling.
/// </summary>
public interface ITaskTimerService
{
    /// <summary>
    /// Executes a timer script and returns the calculated TimerSchedule result.
    /// </summary>
    /// <param name="script">The script code containing the timer logic to evaluate.</param>
    /// <param name="context">The script execution context for timer calculation.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>
    /// A Result containing the TimerSchedule calculated by the timer script execution,
    /// or an error if script execution fails.
    /// </returns>
    Task<Result<TimerSchedule>> ExecuteTimerAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}
