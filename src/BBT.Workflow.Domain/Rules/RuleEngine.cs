namespace BBT.Workflow.Rules;

/// <summary>
/// Defines a contract for a rule engine that can process business rules against a specific context.
/// The rule engine evaluates applicable rules and executes them in sequence.
/// </summary>
/// <typeparam name="T">The type of context that the rules operate on</typeparam>
public interface IRuleEngine<T>
{
    /// <summary>
    /// Processes all applicable rules against the provided context.
    /// The engine will evaluate each rule's applicability and execute those that match.
    /// </summary>
    /// <param name="context">The context to process rules against</param>
    void Process(T context);
    
    /// <summary>
    /// Sets the collection of rules that this engine will use for processing.
    /// This method replaces any previously configured rules.
    /// </summary>
    /// <param name="rules">The collection of rules to be used by the engine</param>
    void SetRules(IEnumerable<IRule<T>> rules);
}

/// <summary>
/// Implements a rule engine that processes business rules against a specific context type.
/// This engine evaluates rules in the order they are provided and executes all applicable rules.
/// Rules are considered applicable based on their IsApplicable method implementation.
/// </summary>
/// <typeparam name="T">The type of context that the rules operate on</typeparam>
/// <param name="rules">Initial collection of rules to be managed by this engine</param>
public class RuleEngine<T>(IEnumerable<IRule<T>> rules) : IRuleEngine<T>
{
    /// <summary>
    /// Internal collection of rules managed by this engine.
    /// </summary>
    private List<IRule<T>> _rules = rules.ToList();

    /// <summary>
    /// Sets the collection of rules that this engine will use for processing.
    /// This method replaces any previously configured rules with the new collection.
    /// </summary>
    /// <param name="rules">The new collection of rules to be used by the engine</param>
    public void SetRules(IEnumerable<IRule<T>> rules)
    {
        _rules = rules.ToList();
    }

    /// <summary>
    /// Processes all applicable rules against the provided context.
    /// The engine iterates through all configured rules, checks their applicability,
    /// and executes those that return true from their IsApplicable method.
    /// Rules are executed in the order they appear in the collection.
    /// </summary>
    /// <param name="context">The context to process rules against</param>
    public void Process(T context)
    {
        foreach (var rule in _rules.Where(r => r.IsApplicable(context)))
        {
            rule.Execute(context);
        }
    }
}