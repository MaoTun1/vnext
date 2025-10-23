using BBT.Workflow.Domain;
using BBT.Workflow.Rules;

namespace BBT.Workflow.Definitions.Rules;

public class FromStateRule(Transition transition): ResultBaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        return !string.IsNullOrEmpty(transition.From) && transition.From != context.Key;
    }

    public override Result Validate(State context)
    {
        return Result.Fail(WorkflowErrors.InvalidState(
            transition.Key,
            transition.From,
            context.Key));
    }
}