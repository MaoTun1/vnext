using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Rules;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Definitions.Rules;

public class ManualTriggerRule(Transition transition, WorkflowExecutionContext executionContext) : BaseRule<State>
{
    public override bool IsApplicable(State context)
    {
        if (transition.TriggerType == TriggerType.Manual)
        {
            return executionContext != WorkflowExecutionContext.User;
        }

        return false;
    }

    public override void Execute(State context)
    {
        throw new UnauthorizedTransitionException(transition.Key, transition.TriggerType, executionContext);
    }
}