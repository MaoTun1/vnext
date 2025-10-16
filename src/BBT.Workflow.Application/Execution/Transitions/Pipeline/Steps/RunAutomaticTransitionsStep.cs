using BBT.Workflow.Domain;
using BBT.Workflow.Execution.ReEntry;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that evaluates and executes automatic transitions.
/// Checks conditions and dispatches qualifying automatic transitions for execution.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class RunAutomaticTransitionsStep() : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Auto;

    /// <inheritdoc />
    public Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target?.AutoTransitions == null || !context.Target.AutoTransitions.Any())
        {
            return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue()));
        }

        foreach (var automaticTransition in context.Target.AutoTransitions)
        {
            var command = ReentryCommand.ForAutomatic(
                context.InstanceId,
                context.Domain,
                context.WorkflowKey,
                automaticTransition.Key,
                context.ExecutionChainId,
                context.ChainDepth,
                context.Headers);
            context.Directives.EnqueueInlineAuto(command);
        }
        
        return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue()));
    }
}
