using BBT.Aether.Results;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Factory for creating appropriate transition handlers based on trigger type.
/// </summary>
public interface ITransitionHandlerFactory
{
    /// <summary>
    /// Gets the appropriate transition handler for the specified trigger type.
    /// </summary>
    /// <param name="triggerType">The type of trigger that initiated the transition.</param>
    /// <returns>The handler capable of processing the specified trigger type.</returns>
    Result<ITransitionHandler> Get(TriggerType triggerType);
}
