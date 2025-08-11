using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when a workflow instance transition is blocked by active SubFlow instances.
/// This exception is raised when attempting to execute a transition while there are incomplete
/// blocking SubFlow (Type "S") instances that must complete before the parent workflow can continue.
/// </summary>
/// <param name="instanceId">The unique identifier of the parent workflow instance</param>
/// <param name="transitionKey">The key of the transition that was attempted</param>
/// <param name="activeSubFlowCount">The number of active blocking SubFlow instances (may be estimated)</param>
public class SubFlowBlockedException(Guid instanceId, string transitionKey, int activeSubFlowCount) 
    : UserFriendlyException(
        code: WorkflowErrorCodes.SubFlowBlocked,
        message: $"Cannot execute transition \"{transitionKey}\" for instance \"{instanceId}\". " +
                $"There {(activeSubFlowCount == 1 ? "is" : "are")} {activeSubFlowCount} active blocking SubFlow instance{(activeSubFlowCount == 1 ? "" : "s")} that must complete first.")
{
    /// <summary>
    /// Gets the unique identifier of the parent workflow instance that is blocked.
    /// </summary>
    public Guid InstanceId { get; } = instanceId;

    /// <summary>
    /// Gets the key of the transition that was attempted to be executed.
    /// </summary>
    public string TransitionKey { get; } = transitionKey;

    /// <summary>
    /// Gets the number of active blocking SubFlow instances that are preventing the transition.
    /// Note: This value may be estimated when exact count is not available for performance reasons.
    /// </summary>
    public int ActiveSubFlowCount { get; } = activeSubFlowCount;
}

/// <summary>
/// Exception thrown when a workflow instance is not found or is in an invalid state for the requested operation.
/// </summary>
/// <param name="instanceId">The unique identifier of the workflow instance</param>
/// <param name="reason">The reason why the instance is invalid or not found</param>
public class InstanceNotFoundException(Guid instanceId, string reason) 
    : UserFriendlyException(
        code: WorkflowErrorCodes.NotFoundInitialState, // Reusing existing code for consistency
        message: $"Instance \"{instanceId}\" {reason}")
{
    /// <summary>
    /// Gets the unique identifier of the workflow instance that was not found.
    /// </summary>
    public Guid InstanceId { get; } = instanceId;

    /// <summary>
    /// Gets the reason why the instance was not found or is invalid.
    /// </summary>
    public string Reason { get; } = reason;
} 