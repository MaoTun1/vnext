using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.States;

/// <summary>
/// Defines the contract for timer execution services that evaluate script-based timer expressions.
/// This service is responsible for executing scripts that return DateTime values for scheduling and timing purposes.
/// </summary>
/// <remarks>
/// Timer execution services are used within workflow state machines to calculate execution times,
/// delays, and scheduled transitions based on business logic defined in scripts.
/// </remarks>
public interface ITimerExecutionService
{
    /// <summary>
    /// Asynchronously executes a timer rule script and returns the calculated DateTime result.
    /// </summary>
    /// <param name="script">
    /// The script code containing the timer logic to evaluate. The script should return a DateTime value
    /// that represents when an action should be executed or a delay should expire.
    /// </param>
    /// <param name="context">
    /// The script execution context containing instance data, workflow information, and other
    /// contextual data needed for timer calculation.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel the operation. Default value is default.
    /// </param>
    /// <returns>
    /// A Task representing the asynchronous operation. The task result contains the DateTime
    /// value calculated by the timer script execution.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the script or context parameter is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the script execution fails or returns an invalid result.
    /// </exception>
    /// <remarks>
    /// The returned DateTime value is used by the workflow engine to schedule future transitions,
    /// calculate delays, or determine when timed events should occur.
    /// </remarks>
    Task<DateTime> ExecuteRuleAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}