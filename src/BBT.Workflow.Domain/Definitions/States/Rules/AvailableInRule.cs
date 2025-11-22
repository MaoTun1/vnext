using BBT.Aether.Results;
using BBT.Workflow.Domain;
using BBT.Workflow.Logging;
using BBT.Workflow.Rules;

namespace BBT.Workflow.Definitions.Rules;

public class AvailableInRule(Transition transition) : ResultBaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        return string.IsNullOrEmpty(transition.From) && !transition.AvailableIn.Contains(context.Key);
    }

    public override Result Validate(State context)
    {
        return Result.Fail(WorkflowErrors.InvalidState(
            transition.Key,
            transition.Target,
            context.Key));
    }
}