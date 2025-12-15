using BBT.Aether.Results;

namespace BBT.Workflow.Rules;

/// <summary>
/// Defines a contract for business rules that return Result instead of throwing exceptions.
/// This interface supports Result Pattern for throw-free validation and business logic.
/// </summary>
/// <typeparam name="T">The type of context that this rule operates on</typeparam>
public interface IResultRule<in T>
{
    /// <summary>
    /// Determines whether this rule should be applied to the given context.
    /// </summary>
    /// <param name="context">The context to evaluate for rule applicability</param>
    /// <returns>True if the rule should be executed for this context; otherwise, false</returns>
    bool IsApplicable(T context);
    
    /// <summary>
    /// Validates the business logic of this rule on the provided context.
    /// Returns Result indicating success or structured error information.
    /// </summary>
    /// <param name="context">The context on which to validate the rule</param>
    /// <returns>Result.Ok() if validation passes, or Result.Fail() with error details</returns>
    Result Validate(T context);
}

/// <summary>
/// Provides a base implementation for Result-based business rules.
/// Derived classes implement throw-free validation using Result Pattern.
/// </summary>
/// <typeparam name="T">The type of context that this rule operates on</typeparam>
public abstract class ResultBaseRule<T> : IResultRule<T>
{
    /// <summary>
    /// Determines whether this rule should be applied to the given context.
    /// </summary>
    /// <param name="context">The context to evaluate for rule applicability</param>
    /// <returns>True if the rule should be executed for this context; otherwise, false</returns>
    public abstract bool IsApplicable(T context);
    
    /// <summary>
    /// Validates the business logic of this rule on the provided context.
    /// Returns Result indicating success or structured error information.
    /// </summary>
    /// <param name="context">The context on which to validate the rule</param>
    /// <returns>Result.Ok() if validation passes, or Result.Fail() with error details</returns>
    public abstract Result Validate(T context);
}

