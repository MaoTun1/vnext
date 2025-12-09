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
public sealed class HandleCancelPreflightStep(
    ILogger<HandleCancelPreflightStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Preflight;

    /// <inheritdoc />
    [Trace]
    public Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(HandleCancelPreflightStep)}");

        // Skip if not a cancel transition
        if (!context.IsCancelTransition())
        {
            return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue()));
        }

        // Railway chain: Log detection -> Validate -> Create skip outcome
        var result = Result.Ok(context)
            .Tap(_ => logger.CancelTransitionDetected(context.InstanceId))
            .Ensure(
                ctx => !ctx.Instance.IsCompleted,
                CreateAlreadyCompletedError(context))
            .Tap(_ => logger.CancelSkipToFinish(context.InstanceId))
            .Map(_ => CreateSkipOutcome());

        return Task.FromResult(result);
    }

    /// <summary>
    /// Creates error for already completed instance.
    /// </summary>
    private Error CreateAlreadyCompletedError(TransitionExecutionContext context)
    {
        logger.CancelInstanceAlreadyCompleted(context.InstanceId, context.Instance.Status.Description);
        return ExecutionErrors.InstanceAlreadyCompleted(context.InstanceId, context.Instance.Status.Description);
    }

    /// <summary>
    /// Creates outcome to skip to CreateTransition step.
    /// </summary>
    private static StepOutcome CreateSkipOutcome()
    {
        return new StepOutcome
        {
            SkipToOrder = LifecycleOrder.CreateTransition
        };
    }
}
