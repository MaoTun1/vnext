using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that detects cancel transitions and short-circuits to HandleFinishStep.
/// This step runs early (Preflight order) to skip normal transition processing for cancellation.
/// </summary>
public sealed class HandleCancelPreflightStep(ILogger<HandleCancelPreflightStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Preflight;

    /// <inheritdoc />
    [Log]
    [Trace]
    public Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(HandleCancelPreflightStep)}");

        // Only process if this is a cancel transition
        if (!context.IsCancelTransition())
        {
            return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue()));
        }

        logger.LogInformation("Cancel transition detected for instance {InstanceId}", context.InstanceId);

        // Validate instance is not already completed
        if (context.Instance.IsCompleted)
        {
            var errorMessage = $"Cannot cancel instance {context.InstanceId}: already in {context.Instance.Status.Description} state";
            logger.LogWarning(errorMessage);
            return Task.FromResult(Result<StepOutcome>.Fail(
                Error.Validation(
                    WorkflowErrorCodes.InvalidState,
                    errorMessage,
                    target: context.InstanceId.ToString())));
        }

        logger.LogInformation(
            "Skipping normal pipeline steps for cancel transition, jumping to Finish step for instance {InstanceId}",
            context.InstanceId);
        
        var outcome = new StepOutcome
        {
            SkipToOrder = LifecycleOrder.CreateTransition
        };
        
        return Task.FromResult(Result<StepOutcome>.Ok(outcome));
    }
}

