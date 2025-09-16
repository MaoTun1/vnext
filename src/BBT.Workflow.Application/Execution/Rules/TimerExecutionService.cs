using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using BBT.Workflow.States;
using BBT.Workflow.Tasks;

namespace BBT.Workflow.Execution.Rules;

/// <summary>
/// Provides timer execution functionality by delegating script execution to the task orchestration service.
/// This service acts as a specialized wrapper around <see cref="ITaskOrchestrationService"/> for timer-based calculations.
/// </summary>
/// <remarks>
/// The <see cref="TimerExecutionService"/> is designed to execute timer logic defined in scripts
/// within the context of workflow state transitions. It leverages the underlying task execution 
/// infrastructure to evaluate timer expressions and return DateTime results that determine scheduling and timing in workflows.
/// </remarks>
public sealed class TimerExecutionService(
    ITaskOrchestrationService taskExecutionService): ITimerExecutionService
{
    /// <inheritdoc />
    public async Task<DateTime> ExecuteRuleAsync(ScriptCode script, ScriptContext context, CancellationToken cancellationToken = default)
    {
        return await taskExecutionService.ExecuteTimerAsync(script, context, cancellationToken);
    }
}