namespace BBT.Workflow.Execution.ReEntry;

/// <summary>
/// Represents the outcome of a re-entry dispatch operation.
/// </summary>
/// <param name="InlineExecuted">Whether the transition was executed inline.</param>
/// <param name="Succeeded">Whether the execution succeeded (only meaningful if InlineExecuted is true).</param>
/// <param name="NewState">The new state after transition, if applicable.</param>
/// <param name="NextTransitionKey">The next transition key to execute, if any.</param>
/// <param name="ResumeFromOrder">The task order to resume from, if applicable.</param>
public sealed record class ReentryOutcome(
    bool InlineExecuted,
    bool Succeeded,
    string? NewState,
    string? NextTransitionKey,
    int? ResumeFromOrder)
{
    /// <summary>
    /// Creates an outcome indicating the transition was not executed (e.g., chain depth exceeded).
    /// </summary>
    public static ReentryOutcome NotExecuted()
        => new(InlineExecuted: false, Succeeded: false, NewState: null, NextTransitionKey: null, ResumeFromOrder: null);

    /// <summary>
    /// Creates an outcome indicating the transition was deferred to a background job.
    /// </summary>
    public static ReentryOutcome Deferred()
        => new(InlineExecuted: false, Succeeded: false, NewState: null, NextTransitionKey: null, ResumeFromOrder: null);

    /// <summary>
    /// Creates an outcome indicating the transition was executed inline with the given success status.
    /// </summary>
    /// <param name="succeeded">Whether the inline execution succeeded.</param>
    public static ReentryOutcome Executed(bool succeeded)
        => new(InlineExecuted: true, Succeeded: succeeded, NewState: null, NextTransitionKey: null, ResumeFromOrder: null);

    /// <summary>
    /// Creates an outcome indicating the transition was executed inline with detailed result.
    /// </summary>
    /// <param name="succeeded">Whether the inline execution succeeded.</param>
    /// <param name="newState">The new state after transition.</param>
    /// <param name="nextTransitionKey">The next transition key, if any.</param>
    /// <param name="resumeFromOrder">The task order to resume from, if applicable.</param>
    public static ReentryOutcome Executed(bool succeeded, string? newState, string? nextTransitionKey = null, int? resumeFromOrder = null)
        => new(InlineExecuted: true, Succeeded: succeeded, NewState: newState, NextTransitionKey: nextTransitionKey, ResumeFromOrder: resumeFromOrder);
}