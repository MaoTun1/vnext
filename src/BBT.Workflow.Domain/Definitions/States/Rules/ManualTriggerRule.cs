using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Rules;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Definitions.Rules;

public class ManualTriggerRule(Transition transition, ExecutionActor executionActor) : BaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        if (transition.TriggerType == TriggerType.Manual)
        {
            return executionActor != ExecutionActor.User;
        }

        return false;
    }

    public override void Execute(State context)
    {
        throw new UnauthorizedTransitionException(transition.Key, transition.TriggerType, executionActor);
    }
}