using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Timer;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.States;

/// <summary>
/// Defines the contract for timer execution services that evaluate script-based timer expressions.
/// This service is responsible for executing scripts that return flexible timer schedules for scheduling and timing purposes.
/// </summary>
/// <remarks>
/// Timer execution services are used within workflow state machines to calculate execution times,
/// delays, and scheduled transitions based on business logic defined in scripts.
/// The enhanced service supports Dapr-compatible scheduling including DateTime, Cron expressions, Duration, and Immediate execution.
/// </remarks>
public interface ITimerExecutionService
{
    /// <summary>
    /// Asynchronously executes a timer rule script and returns the calculated WorkflowTimerSchedule result.
    /// </summary>
    /// <param name="script">
    /// The script code containing the timer logic to evaluate. The script should return a WorkflowTimerSchedule
    /// that represents when and how an action should be executed or a delay should expire.
    /// </param>
    /// <param name="context">
    /// The script execution context containing instance data, workflow information, and other
    /// contextual data needed for timer calculation.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel the operation. Default value is default.
    /// </param>
    /// <returns>
    /// A Task representing the asynchronous operation. The task result contains the WorkflowTimerSchedule
    /// calculated by the timer script execution.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the script or context parameter is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the script execution fails or returns an invalid result.
    /// </exception>
    /// <remarks>
    /// The returned WorkflowTimerSchedule is used by the workflow engine to schedule future transitions,
    /// calculate delays, or determine when timed events should occur. It provides the same flexibility
    /// as Dapr's job scheduling system.
    /// </remarks>
    Task<TimerSchedule> ExecuteRuleAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}