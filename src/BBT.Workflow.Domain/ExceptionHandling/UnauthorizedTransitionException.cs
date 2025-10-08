using BBT.Aether;
using BBT.Workflow.Definitions;
using BBT.Workflow.Shared;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when a transition cannot be executed by the current execution context
/// </summary>
public class UnauthorizedTransitionException(
    string transitionKey,
    TriggerType triggerType,
    ExecutionActor executionActor)
    : UserFriendlyException(WorkflowErrorCodes.UnauthorizedTransition,
        $"Transition '{transitionKey}' with trigger type '{triggerType}' cannot be executed by '{executionActor}' context")
{
    public string TransitionKey { get; } = transitionKey;
    public TriggerType TriggerType { get; } = triggerType;
    public ExecutionActor ExecutionActor { get; } = executionActor;
}
