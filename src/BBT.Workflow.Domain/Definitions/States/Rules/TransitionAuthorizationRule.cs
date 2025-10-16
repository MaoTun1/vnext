using BBT.Workflow.Domain;
using BBT.Workflow.Rules;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Definitions.Rules;

/// <summary>
/// Validates that the transition can be executed by the specified execution context
/// </summary>
public class TransitionAuthorizationRule(Transition transition, ExecutionActor executionActor)
    : ResultBaseRule<State>
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

    public override Result Validate(State context)
    {
        return Result.Fail(WorkflowErrors.TransitionUnauthorized(
            transition.Key,
            transition.TriggerType,
            executionActor));
    }
}