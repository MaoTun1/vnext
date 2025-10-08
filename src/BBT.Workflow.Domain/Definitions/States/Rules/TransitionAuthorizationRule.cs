using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Rules;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Definitions.Rules;

/// <summary>
/// Validates that the transition can be executed by the specified execution context
/// </summary>
public class TransitionAuthorizationRule(Transition transition, ExecutionActor executionActor)
    : BaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        if (transition.TriggerType == TriggerType.Automatic
            ||
            transition.TriggerType == TriggerType.Scheduled)
        {
            return executionActor != ExecutionActor.System;
        }
        
        return false;
    }

    public override void Execute(State context)
    {
        throw new UnauthorizedTransitionException(
            transition.Key,
            transition.TriggerType,
            executionActor);
    }
}