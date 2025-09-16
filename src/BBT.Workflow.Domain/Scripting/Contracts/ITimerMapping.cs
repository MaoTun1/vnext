namespace BBT.Workflow.Scripting;

/// <summary>
/// Defines the contract for timer mapping handlers that process script contexts to calculate DateTime values.
/// This interface is used for implementing custom timer logic within the workflow scripting engine.
/// </summary>
/// <remarks>
/// Timer mapping implementations are responsible for interpreting script context data
/// and returning appropriate DateTime values for scheduling, delays, and time-based workflow operations.
/// </remarks>
public interface ITimerMapping
{
    /// <summary>
    /// Asynchronously handles timer calculation based on the provided script context.
    /// </summary>
    /// <param name="context">
    /// The script execution context containing instance data, workflow information,
    /// and other contextual data needed for timer calculation. This context provides
    /// access to workflow variables, instance state, and execution metadata.
    /// </param>
    /// <returns>
    /// A Task representing the asynchronous operation. The task result contains the DateTime
    /// value calculated based on the script context data and timer logic.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the context parameter is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the timer calculation fails or produces an invalid result.
    /// </exception>
    /// <remarks>
    /// Implementations should handle various timer scenarios such as:
    /// <list type="bullet">
    /// <item>Absolute scheduling (specific date/time)</item>
    /// <item>Relative delays (time from now)</item>
    /// <item>Business logic-based timing (calculated from workflow data)</item>
    /// <item>Recurring patterns (periodic execution)</item>
    /// </list>
    /// </remarks>
    Task<DateTime> Handler(ScriptContext context);
}