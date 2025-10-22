using BBT.Workflow.Definitions;
using BBT.Workflow.Execution;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Defines the contract for handling different types of transition triggers.
/// Each handler is responsible for pre and post processing logic specific to its trigger type.
/// </summary>
public interface ITransitionHandler
{
    /// <summary>
    /// Determines if this handler can process the specified trigger type.
    /// </summary>
    /// <param name="triggerType">The type of trigger that initiated the transition.</param>
    /// <returns>True if this handler can process the trigger type; otherwise, false.</returns>
    bool CanHandle(TriggerType triggerType);
    
    /// <summary>
    /// Performs pre-processing logic before the transition pipeline executes.
    /// This may include validation, authorization, condition checking, etc.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PreHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
    
    /// <summary>
    /// Performs post-processing logic after the transition pipeline executes.
    /// This may include cleanup, notifications, metrics recording, etc.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PostHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}
