using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Validation;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Handler for event-driven transitions triggered by external events or messages.
/// Manages event validation and correlation with workflow instances.
/// </summary>
public sealed class EventTransitionHandler(
    ILogger<EventTransitionHandler> logger,
    ITransitionValidationService validationService) : TransitionHandlerBase(logger, validationService)
{
    /// <inheritdoc />
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Event;
}
