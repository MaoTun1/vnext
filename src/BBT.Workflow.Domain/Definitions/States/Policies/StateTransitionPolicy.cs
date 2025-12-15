using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Definitions.Rules;
using BBT.Workflow.Rules;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Instances.Policies;

/// <summary>
/// Validates state transition rules using Result Pattern.
/// Provides throw-free validation for workflow transitions.
/// </summary>
public class StateTransitionPolicy
{
    private readonly IResultRuleEngine<State> _resultRuleEngine;

    /// <summary>
    /// Initializes with Result-based rule engine.
    /// </summary>
    public StateTransitionPolicy(IResultRuleEngine<State> resultRuleEngine)
    {
        _resultRuleEngine = resultRuleEngine;
    }

    /// <summary>
    /// Validates state transition rules using Result Pattern.
    /// Returns Result.Ok() if all validations pass, otherwise returns first validation error.
    /// </summary>
    /// <param name="currentState">Current state of the instance</param>
    /// <param name="transition">Transition to validate</param>
    /// <param name="executionActor">Actor executing the transition</param>
    /// <returns>Result indicating validation success or failure</returns>
    public Result Validate(State currentState, Transition transition, ExecutionActor executionActor = ExecutionActor.User)
    {
        var rules = new List<ResultBaseRule<State>>();

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

        _resultRuleEngine.SetRules(rules);
        return _resultRuleEngine.Validate(currentState);
    }
}