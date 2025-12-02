using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Validation;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Handler for scheduled transitions triggered by timers or cron expressions.
/// Manages scheduled execution and validates timing constraints.
/// </summary>
public sealed class ScheduledTransitionHandler(
    ILogger<ScheduledTransitionHandler> logger,
    ITransitionValidationService validationService) : TransitionHandlerBase(logger, validationService)
{
    /// <inheritdoc />
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Scheduled;

    /// <inheritdoc />
    protected override async Task PreValidateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // For scheduled transitions that are being enqueued (not executed immediately),
        // we skip immediate execution
        if (ShouldSkipImmediateExecution(context))
        {
            context.SkipImmediateExecution = true;
            return;
        }

        // For scheduled transitions that are being executed (from background jobs),
        // validate the execution timing and constraints
        await ValidateExecutionTimingAsync(context, cancellationToken);
    }

    /// <summary>
    /// Determines if this scheduled transition should skip immediate execution.
    /// </summary>
    private static bool ShouldSkipImmediateExecution(TransitionExecutionContext context)
    {
        // If this is not a re-entry (i.e., it's the initial scheduling), skip immediate execution
        return !context.IsReentry;
    }

    /// <summary>
    /// Validates the execution timing for scheduled transitions.
    /// </summary>
    private async Task ValidateExecutionTimingAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Basic validation: ensure this is actually a re-entry execution
        if (!context.IsReentry)
        {
            throw new InvalidOperationException(
                $"Scheduled transition {context.TransitionKey} should only be executed as re-entry");
        }
        await Task.CompletedTask;
    }
}
