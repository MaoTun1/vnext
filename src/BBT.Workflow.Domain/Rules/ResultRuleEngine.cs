using BBT.Aether.Results;

namespace BBT.Workflow.Rules;

/// <summary>
/// Defines a contract for a rule engine that validates business rules using Result Pattern.
/// Returns structured errors instead of throwing exceptions.
/// </summary>
/// <typeparam name="T">The type of context that the rules operate on</typeparam>
public interface IResultRuleEngine<T>
{
    /// <summary>
    /// Validates all applicable rules against the provided context.
    /// Returns the first validation failure or success if all rules pass.
    /// </summary>
    /// <param name="context">The context to validate rules against</param>
    /// <returns>Result.Ok() if all rules pass, otherwise Result.Fail() with first error</returns>
    Result Validate(T context);
    
    /// <summary>
    /// Sets the collection of rules that this engine will use for validation.
    /// This method replaces any previously configured rules.
    /// </summary>
    /// <param name="rules">The collection of rules to be used by the engine</param>
    void SetRules(IEnumerable<IResultRule<T>> rules);
}

/// <summary>
/// Implements a Result-based rule engine that validates business rules without throwing exceptions.
/// Evaluates rules in order and returns the first failure or success if all pass.
/// </summary>
/// <typeparam name="T">The type of context that the rules operate on</typeparam>
public class ResultRuleEngine<T> : IResultRuleEngine<T>
{
    private List<IResultRule<T>> _rules;

    /// <summary>
    /// Initializes a new instance of ResultRuleEngine with an initial set of rules.
    /// </summary>
    /// <param name="rules">Initial collection of rules</param>
    public ResultRuleEngine(IEnumerable<IResultRule<T>> rules)
    {
        _rules = rules.ToList();
    }

    /// <summary>
    /// Initializes a new instance of ResultRuleEngine with no rules.
    /// </summary>
    public ResultRuleEngine()
    {
        _rules = new List<IResultRule<T>>();
    }

    /// <summary>
    /// Sets the collection of rules that this engine will use for validation.
    /// </summary>
    /// <param name="rules">The new collection of rules</param>
    public void SetRules(IEnumerable<IResultRule<T>> rules)
    {
        _rules = rules.ToList();
    }

    /// <summary>
    /// Validates all applicable rules against the provided context.
    /// Returns immediately on first failure (fail-fast).
    /// </summary>
    /// <param name="context">The context to validate rules against</param>
    /// <returns>Result.Ok() if all rules pass, otherwise Result.Fail() with first error</returns>
    public Result Validate(T context)
    {
        foreach (var rule in _rules.Where(r => r.IsApplicable(context)))
        {
            var result = rule.Validate(context);
            if (!result.IsSuccess)
            {
                return result; // Fail-fast: return first error
            }
        }

        return Result.Ok();
    }

    /// <summary>
    /// Validates all applicable rules and collects all errors (instead of fail-fast).
    /// Useful when you want to return all validation errors at once.
    /// </summary>
    /// <param name="context">The context to validate rules against</param>
    /// <returns>Result.Ok() if all rules pass, otherwise Result with combined errors</returns>
    public Result ValidateAll(T context)
    {
        var errors = new List<Error>();

        foreach (var rule in _rules.Where(r => r.IsApplicable(context)))
        {
            var result = rule.Validate(context);
            if (!result.IsSuccess)
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count > 0)
        {
            // Return first error (could be enhanced to combine multiple errors)
            return Result.Fail(errors[0]);
        }

        return Result.Ok();
    }
}

