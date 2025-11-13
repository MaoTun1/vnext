using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Execution.TriggerTransition;

/// <summary>
/// Defines the contract for trigger transition execution strategies.
/// Implements the Strategy Pattern to handle different trigger transition types (Start, Trigger, SubProcess).
/// </summary>
public interface ITriggerTransitionStrategy
{
    /// <summary>
    /// Executes the trigger transition using the strategy's specific implementation.
    /// </summary>
    /// <param name="task">The trigger transition task containing configuration.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken);
}

