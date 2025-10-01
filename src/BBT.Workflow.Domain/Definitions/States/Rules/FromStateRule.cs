using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Rules;

namespace BBT.Workflow.Definitions.Rules;

public class FromStateRule(Transition transition): BaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        return !string.IsNullOrEmpty(transition.From) && transition.From != context.Key;
    }

    public override void Execute(State context)
    {
        throw new InvalidStateException(transition.Key, transition.Target, context.Key);
    }
}