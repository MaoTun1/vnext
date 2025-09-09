using BBT.Aether;
using BBT.Workflow.Definitions;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when a transition cannot be executed by the current execution context
/// </summary>
public class UnauthorizedTransitionException(
    string transitionKey,
    TriggerType triggerType,
    WorkflowExecutionContext executionContext)
    : UserFriendlyException(WorkflowErrorCodes.UnauthorizedTransition,
        $"Transition '{transitionKey}' with trigger type '{triggerType}' cannot be executed by '{executionContext}' context")
{
    public string TransitionKey { get; } = transitionKey;
    public TriggerType TriggerType { get; } = triggerType;
    public WorkflowExecutionContext ExecutionContext { get; } = executionContext;
}
