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
        // Always apply this rule for authorization check
        return true;
    }

    public override void Execute(State context)
    {
        // User context can only execute Manual and Event transitions
        if (executionContext == WorkflowExecutionContext.User
            && (transition.TriggerType == TriggerType.Automatic
                ||
                transition.TriggerType == TriggerType.Scheduled)
           )
        {
            throw new UnauthorizedTransitionException(
                transition.Key,
                transition.TriggerType,
                executionContext);
        }

        // System context can execute any transition type
        // No additional validation needed for system context
    }
}