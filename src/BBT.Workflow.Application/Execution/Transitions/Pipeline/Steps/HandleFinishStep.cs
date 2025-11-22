using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that handles workflow finishing.
/// Manages workflow completion when the target state type is Finish.
/// This step runs after all other pipeline steps to ensure proper completion.
/// Uses Result pattern for exception-free error handling.
/// SubItem completion events are now published as domain events via Instance.Complete().
/// </summary>
public sealed class HandleFinishStep(
    IInstanceRepository instanceRepository,
    ILogger<HandleFinishStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Finish;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(HandleFinishStep)}");

        // Check if this is a cancel transition
        var isCancelTransition = context.IsCancelTransition();
        
        // For cancel transitions, we don't need a Finish state type - we cancel immediately
        if (!isCancelTransition)
        {
            if (context.Target == null)
            {
                logger.LogWarning("Target state is null for instance {InstanceId}", context.InstanceId);
                return Result<StepOutcome>.Ok(StepOutcome.Continue());
            }

            // Only handle Finish state types for normal completions
            if (context.Target.StateType != StateType.Finish)
            {
                return Result<StepOutcome>.Ok(StepOutcome.Continue());
            }
        }
        
        // Railway Oriented Programming: Chain operations, each wrapped in Try
        var updateResult = await UpdateInstanceStatus(context, isCancelTransition, cancellationToken);
        if (!updateResult.IsSuccess)
            return Result<StepOutcome>.Fail(updateResult.Error);

        var markResult = await MarkFinishStateInContext(context);
        if (!markResult.IsSuccess)
            return Result<StepOutcome>.Fail(markResult.Error);

        // Domain events are automatically published by Instance.Complete() and Instance.Cancel()
        // No need for additional pub/sub publishing here
        
        return Result<StepOutcome>.Ok(StepOutcome.Continue());
    }

    /// <summary>
    /// Updates the instance status in repository.
    /// </summary>
    private async Task<Result> UpdateInstanceStatus(
        TransitionExecutionContext context, 
        bool isCancelTransition, 
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                if (isCancelTransition)
                {
                    logger.LogInformation("Canceling instance {InstanceId}", context.Instance.Id);
                    context.Instance.Cancel();
                }
                else
                {
                    logger.LogInformation("Completing instance {InstanceId}", context.Instance.Id);
                    context.Instance.Complete();
                }
                // TODO: UpdateStatus'dan Update'e geçtik.
                await instanceRepository.UpdateAsync(context.Instance, true, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Marks that we're in a finish state in context items.
    /// </summary>
    private Task<Result> MarkFinishStateInContext(TransitionExecutionContext context)
    {
        context.Items["IsFinishState"] = true;
        return Task.FromResult(
            result: Result.Ok()
        );
    }
}
