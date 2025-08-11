namespace BBT.Workflow.Definitions;

/// <summary>
/// Main state types
/// </summary>
public enum StateType
{
    /// <summary>
    /// Starting state of the workflow
    /// </summary>
    Initial = 1,

    /// <summary>
    /// Middle states where work is being done
    /// </summary>
    Intermediate = 2,

    /// <summary>
    /// End states of the workflow
    /// </summary>
    Finish = 3,

    /// <summary>
    /// State that executes another workflow
    /// </summary>
    SubFlow = 4
}

/// <summary>
/// Subtypes
/// </summary>
public enum StateSubType
{
    /// <summary>
    /// No specific subtype
    /// </summary>
    None = 0,

    /// <summary>
    /// Successful completion
    /// </summary>
    Success = 1,

    /// <summary>
    /// Error condition
    /// </summary>
    Error = 2,

    /// <summary>
    /// Manually terminated
    /// </summary>
    Terminated = 3,

    /// <summary>
    /// Temporarily suspended
    /// </summary>
    Suspended = 4
}