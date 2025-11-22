using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using Microsoft.Extensions.DependencyInjection;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Factory implementation for creating appropriate transition handlers based on trigger type.
/// Uses dependency injection to resolve handlers and provides fallback behavior.
/// </summary>
public sealed class TransitionHandlerFactory(
    IServiceProvider serviceProvider) : ITransitionHandlerFactory
{
    /// <inheritdoc />
    public Result<ITransitionHandler> Get(TriggerType triggerType)
    {
        // Get all registered transition handlers
        var handlers = serviceProvider.GetServices<ITransitionHandler>();

        // Find the handler that can handle this trigger type
        var handler = handlers.FirstOrDefault(h => h.CanHandle(triggerType));

        return handler != null
            ? Result<ITransitionHandler>.Ok(handler)
            : Result<ITransitionHandler>.Fail(
                Error.NotSupported(WorkflowErrorCodes.TransitionHandlerNotSupported,
                    $"No transition handler found for trigger type {triggerType}"
                )
            );
    }
}