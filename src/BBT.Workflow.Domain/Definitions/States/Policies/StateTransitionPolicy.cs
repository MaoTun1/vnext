using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Rules;
using BBT.Workflow.Rules;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Instances.Policies;

public class StateTransitionPolicy(IRuleEngine<State> ruleEngine)
{
    public void Validate(State currentState, Transition transition, WorkflowExecutionContext executionContext = WorkflowExecutionContext.User)
    {
        var rules = new List<BaseRule<State>>();

        if (!transition.From.IsNullOrEmpty())
        {
            rules.Add(new FromStateRule(transition));
        }

        if (transition.AvailableIn.Any())
        {
            rules.Add(new AvailableInRule(transition));
        }

        rules.Add(new ManualTriggerRule(transition));
        
        rules.Add(new TransitionAuthorizationRule(transition, executionContext));

        ruleEngine.SetRules(rules);
        ruleEngine.Process(currentState);
    }
}