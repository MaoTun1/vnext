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
}
