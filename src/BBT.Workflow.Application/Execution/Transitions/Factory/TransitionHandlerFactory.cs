using BBT.Workflow.Definitions;
using BBT.Workflow.Execution;
using BBT.Workflow.Execution.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Factory implementation for creating appropriate transition handlers based on trigger type.
/// Uses dependency injection to resolve handlers and provides fallback behavior.
/// </summary>
public sealed class TransitionHandlerFactory(
    IServiceProvider serviceProvider,
    ILogger<TransitionHandlerFactory> logger) : ITransitionHandlerFactory
{
    /// <inheritdoc />
    public ITransitionHandler Get(TriggerType triggerType)
    {
        logger.LogDebug("Resolving transition handler for trigger type {TriggerType}", triggerType);

        // Get all registered transition handlers
        var handlers = serviceProvider.GetServices<ITransitionHandler>();

        // Find the handler that can handle this trigger type
        var handler = handlers.FirstOrDefault(h => h.CanHandle(triggerType));

        if (handler == null)
        {
            logger.LogError("No transition handler found for trigger type {TriggerType}", triggerType);
            throw new NotSupportedException($"No transition handler found for trigger type {triggerType}");
        }

        logger.LogDebug("Resolved transition handler {HandlerType} for trigger type {TriggerType}",
            handler.GetType().Name, triggerType);

        return handler;
    }
}
