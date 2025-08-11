using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using BBT.Workflow.States;

namespace BBT.Workflow.Execution.Rules;

/// <summary>
/// Provides rule execution functionality by delegating script execution to the task execution service.
/// This service acts as a specialized wrapper around <see cref="ITaskOrchestrationService"/> for rule-based conditions.
/// </summary>
/// <remarks>
/// The <see cref="RuleExecutionService"/> is designed to execute conditional logic defined in scripts
/// within the context of workflow state transitions. It leverages the underlying task execution 
/// infrastructure to evaluate business rules and return boolean results that determine workflow flow.
/// </remarks>
public sealed class RuleExecutionService(
    ITaskOrchestrationService taskExecutionService) : IRuleExecutionService
{
    /// <inheritdoc />
    public async Task<bool> ExecuteRuleAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        return await taskExecutionService.ExecuteConditionAsync(script, context, cancellationToken);
    }
} 