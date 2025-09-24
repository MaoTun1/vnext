using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Service interface for handling automatic workflow transitions.
/// Provides functionality to check and execute automatic transitions without circular dependencies.
/// </summary>
public interface IAutoTransitionService
{
    /// <summary>
    /// Checks for automatic transitions and executes them sequentially until the first successful one.
    /// This method processes automatic transitions in order, ensuring only one transition executes
    /// to prevent state conflicts. Processing stops after the first successful transition.
    /// </summary>
    /// <param name="workflow">The workflow definition.</param>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="AutoTransitionFailedException">Thrown when all automatic transitions fail.</exception>
    Task CheckAndExecuteAutomaticTransitionsAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default);
}
