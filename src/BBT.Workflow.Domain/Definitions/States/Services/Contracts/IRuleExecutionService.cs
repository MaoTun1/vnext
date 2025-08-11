using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.States;

/// <summary>
/// Provides rule execution functionality by delegating script execution to the task execution service.
/// This service acts as a specialized wrapper around <see cref="TaskExecutionService"/> for rule-based conditions.
/// </summary>
/// <remarks>
/// The <see cref="RuleExecutionService"/> is designed to execute conditional logic defined in scripts
/// within the context of workflow state transitions. It leverages the underlying task execution 
/// infrastructure to evaluate business rules and return boolean results that determine workflow flow.
/// </remarks>
public interface IRuleExecutionService
{
    /// <summary>
    /// Executes a rule script and returns the result
    /// </summary>
    /// <param name="script">The script code to execute</param>
    /// <param name="context">The script execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the rule passes, false otherwise</returns>
    Task<bool> ExecuteRuleAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
} 