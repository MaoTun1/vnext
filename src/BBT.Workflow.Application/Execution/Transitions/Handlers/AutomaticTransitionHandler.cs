using BBT.Aether;
using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Validation;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Handler for automatic transitions triggered by the system based on conditions.
/// Validates conditions before allowing transition execution.
/// </summary>
public sealed class AutomaticTransitionHandler(
    ITransitionValidationService validationService,
    ILogger<AutomaticTransitionHandler> logger) : TransitionHandlerBase(logger, validationService)
{
    /// <inheritdoc />
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Automatic;

    protected override async Task PreValidateAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        await ValidateSystemStateAsync(context);
        await base.PreValidateAsync(context, cancellationToken);
    }

    /// <summary>
    /// Validates the system state for automatic transition execution.
    /// </summary>
    private Task ValidateSystemStateAsync(TransitionExecutionContext context)
    {
        // Basic validation: ensure we're not exceeding chain depth limits
        // (This is also checked in the dispatcher, but we double-check here)
        const int maxChainDepth = 50; // This should come from configuration
        if (context.ChainDepth > maxChainDepth)
        {
            //TODO: Spesifik Exception
            throw new UserFriendlyException(
                $"Maximum chain depth ({maxChainDepth}) exceeded for automatic transition {context.TransitionKey}",
                WorkflowErrorCodes.ExecutionStepFailed);
        }

        return Task.CompletedTask;
    }
}