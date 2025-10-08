using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Rules;
using BBT.Workflow.Rules;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Instances.Policies;

public class StateTransitionPolicy(IRuleEngine<State> ruleEngine)
{
    public void Validate(State currentState, Transition transition, ExecutionActor executionActor = ExecutionActor.User)
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

        rules.Add(new ManualTriggerRule(transition, executionActor));
        
        rules.Add(new TransitionAuthorizationRule(transition, executionActor));

        ruleEngine.SetRules(rules);
        ruleEngine.Process(currentState);
    }
}