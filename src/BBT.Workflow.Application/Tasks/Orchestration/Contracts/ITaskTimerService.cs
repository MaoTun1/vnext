using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Timer;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks;

public interface ITaskTimerService
{
    /// <summary>
    /// Executes a timer script and returns the calculated WorkflowTimerSchedule result.
    /// </summary>
    /// <param name="script">The script code containing the timer logic to evaluate.</param>
    /// <param name="context">The script execution context for timer calculation.</param>
    /// <param name="cancellationToken">Cancellation token for async operation control.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains 
    /// the WorkflowTimerSchedule calculated by the timer script execution.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when script or context is null.</exception>
    Task<TimerSchedule> ExecuteTimerAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}