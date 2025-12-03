using BBT.Aether.Results;
using BBT.Workflow.Domain;
using BBT.Workflow.Logging;
using BBT.Workflow.Rules;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Definitions.Rules;

public class ManualTriggerRule(Transition transition, ExecutionActor executionActor) : ResultBaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        if (transition.TriggerType == TriggerType.Manual)
        {
            return executionActor != ExecutionActor.User;
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