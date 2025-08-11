namespace BBT.Workflow.Rules;

/// <summary>
/// Defines a contract for implementing business rules that can be applied to a specific context.
/// This interface provides a standardized way to create reusable and testable business logic components.
/// </summary>
/// <typeparam name="T">The type of context that this rule operates on</typeparam>
public interface IRule<in T>
{
    /// <summary>
    /// Determines whether this rule should be applied to the given context.
    /// This method allows for conditional rule execution based on the context state.
    /// </summary>
    /// <param name="context">The context to evaluate for rule applicability</param>
    /// <returns>True if the rule should be executed for this context; otherwise, false</returns>
    bool IsApplicable(T context);
    
    /// <summary>
    /// Executes the business logic of this rule on the provided context.
    /// This method should only be called after confirming that the rule is applicable via IsApplicable method.
    /// </summary>
    /// <param name="context">The context on which to execute the rule</param>
    void Execute(T context);
}

