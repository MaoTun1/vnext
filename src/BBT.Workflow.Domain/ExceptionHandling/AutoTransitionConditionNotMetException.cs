using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when an automatic transition condition is not met.
/// This is a special exception used internally to handle multi-auto-transition scenarios
/// where some transitions may fail condition checks while others succeed.
/// </summary>
public sealed class AutoTransitionConditionNotMetException : UserFriendlyException
{
    /// <summary>
    /// Initializes a new instance of the AutoTransitionConditionNotMetException class.
    /// </summary>
    public AutoTransitionConditionNotMetException()
        : base("Auto-transition condition not met", WorkflowErrorCodes.AutoTransitionConditionNotMet)
    {
    }
}

