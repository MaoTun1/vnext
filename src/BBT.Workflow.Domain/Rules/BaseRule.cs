namespace BBT.Workflow.Rules;

/// <summary>
/// Provides a base implementation for business rules that operate on a specific context type.
/// This abstract class implements the IRule interface and serves as a foundation for concrete rule implementations.
/// Derived classes must implement the IsApplicable and Execute methods to define their specific business logic.
/// </summary>
/// <typeparam name="T">The type of context that this rule operates on</typeparam>
public abstract class BaseRule<T> : IRule<T>
{
    /// <summary>
    /// Determines whether this rule should be applied to the given context.
    /// Derived classes must implement this method to define their specific applicability criteria.
    /// </summary>
    /// <param name="context">The context to evaluate for rule applicability</param>
    /// <returns>True if the rule should be executed for this context; otherwise, false</returns>
    public abstract bool IsApplicable(T context);
    
    /// <summary>
    /// Executes the business logic of this rule on the provided context.
    /// Derived classes must implement this method to define their specific execution behavior.
    /// </summary>
    /// <param name="context">The context on which to execute the rule</param>
    public abstract void Execute(T context);
}