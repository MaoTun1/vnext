using BBT.Aether.Results;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Factory implementation for creating appropriate transition handlers based on trigger type.
/// Uses dependency injection to resolve handlers and provides fallback behavior.
/// </summary>
public sealed class TransitionHandlerFactory(
    IEnumerable<ITransitionHandler> handlers) : ITransitionHandlerFactory
{
    /// <inheritdoc />
    public Result<ITransitionHandler> Get(TriggerType triggerType)
    {
        // Get all registered transition handlers
        var handler = handlers.FirstOrDefault(s => s.CanHandle(triggerType));
        
        return handler != null
            ? Result<ITransitionHandler>.Ok(handler)
            : Result<ITransitionHandler>.Fail(
                Error.NotSupported(WorkflowErrorCodes.TransitionHandlerNotSupported,
                    $"No transition handler found for trigger type {triggerType}"
                )
            );
    }
}