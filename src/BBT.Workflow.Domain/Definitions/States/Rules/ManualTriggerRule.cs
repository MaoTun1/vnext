using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Rules;

namespace BBT.Workflow.Definitions.Rules;

public class ManualTriggerRule(Transition transition) : BaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        return context.Key == transition.Target &&
               context.StateType != StateType.Initial;
    }

    public override void Execute(State context)
    {
        throw new InvalidStateException(transition.Key, transition.Target, context.Key);
    }
}