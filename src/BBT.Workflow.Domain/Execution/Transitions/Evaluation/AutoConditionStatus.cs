namespace BBT.Workflow.Execution;

/// <summary>
/// Represents the possible status outcomes of an automatic transition condition evaluation.
/// Distinguishes between satisfied conditions, unsatisfied conditions, and evaluation failures.
/// </summary>
public enum AutoConditionStatus
{
    /// <summary>
    /// The condition evaluated to true. The automatic transition is eligible for execution.
    /// </summary>
    Satisfied,

    /// <summary>
    /// The condition evaluated to false. This is a normal business outcome, not an error.
    /// The automatic transition should not be executed.
    /// </summary>
    NotSatisfied,

    /// <summary>
    /// The condition evaluation failed due to a technical error (e.g., script compilation error,
    /// missing configuration, or runtime exception). This represents an actual error condition.
    /// </summary>
    Failed
}

