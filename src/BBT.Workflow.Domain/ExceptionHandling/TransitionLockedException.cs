using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when a transition is already in progress for a workflow instance.
/// This exception prevents concurrent transition execution on the same instance.
/// </summary>
/// <param name="instanceId">The unique identifier of the workflow instance</param>
/// <param name="transitionKey">The key of the transition that was attempted</param>
public class TransitionLockedException(Guid instanceId, string transitionKey) 
    : UserFriendlyException(
        code: WorkflowErrorCodes.TransitionLocked,
        message: $"A transition is already in progress for instance \"{instanceId}\". " +
                $"Cannot execute transition \"{transitionKey}\" until the current transition completes.")
{
    /// <summary>
    /// Gets the unique identifier of the workflow instance that has a transition in progress.
    /// </summary>
    public Guid InstanceId { get; } = instanceId;

    /// <summary>
    /// Gets the key of the transition that was attempted to be executed.
    /// </summary>
    public string TransitionKey { get; } = transitionKey;
} 