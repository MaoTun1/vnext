using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Rules;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Definitions.Rules;

/// <summary>
/// Validates that the transition can be executed by the specified execution context
/// </summary>
public class TransitionAuthorizationRule(Transition transition, WorkflowExecutionContext executionContext)
    : BaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        if (transition.TriggerType == TriggerType.Automatic
            ||
            transition.TriggerType == TriggerType.Scheduled)
        {
            return executionContext != WorkflowExecutionContext.System;
        }
        
        return false;
    }

    public override void Execute(State context)
    {
        throw new UnauthorizedTransitionException(
            transition.Key,
            transition.TriggerType,
            executionContext);
    }
}