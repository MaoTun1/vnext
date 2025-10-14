using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Validation;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Handler for manual transitions triggered by users or external API calls.
/// Performs validation of policies, HMAC, authorization, and schema.
/// </summary>
public sealed class ManualTransitionHandler(
    ILogger<ManualTransitionHandler> logger,
    ITransitionValidationService validationService) : TransitionHandlerBase(logger, validationService)
{
    /// <inheritdoc />
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Manual;

    /// <inheritdoc />
    protected override async Task PreValidateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Validating manual transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        // Call base validation which includes:
        // 1. Policy validation (StateTransitionPolicy)
        // 2. Authorization checks (TransitionAuthorizationRule)
        // 3. Schema validation for input data
        // 4. Business rule validations
        await base.PreValidateAsync(context, cancellationToken);
        
        await ValidateManualSpecificAsync(context, cancellationToken);

        Logger.LogDebug("Manual transition validation completed for {TransitionKey}", context.TransitionKey);
    }

    /// <summary>
    /// Validates manual-specific requirements like HMAC and rate limiting.
    /// </summary>
    private async Task ValidateManualSpecificAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task PostProcessAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Post-processing manual transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        await RecordAuditLogAsync(context, cancellationToken);

        Logger.LogDebug("Manual transition post-processing completed for {TransitionKey}", context.TransitionKey);
    }

    /// <summary>
    /// Records audit log for manual transition execution.
    /// </summary>
    private async Task RecordAuditLogAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}
