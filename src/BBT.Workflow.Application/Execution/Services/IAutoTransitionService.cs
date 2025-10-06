using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;

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

    /// <summary>
    /// Checks for automatic transitions and executes them sequentially, returning execution results.
    /// This method processes automatic transitions in order and returns information about execution
    /// including the refreshed instance state to avoid unnecessary database queries.
    /// </summary>
    /// <param name="workflow">The workflow definition.</param>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>AutoTransitionResult containing execution status and refreshed instance.</returns>
    /// <exception cref="AutoTransitionFailedException">Thrown when all automatic transitions fail.</exception>
    Task<AutoTransitionResult> CheckAndExecuteAutomaticTransitionsWithResultAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes both automatic transitions and scheduled transitions in sequence, returning the final result.
    /// This method combines auto transition execution with scheduled transition handling to reduce
    /// code duplication and provide a unified execution flow.
    /// </summary>
    /// <param name="workflow">The workflow definition.</param>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="scriptContext">The script context for scheduled transitions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>AutoTransitionResult containing execution status and final instance state.</returns>
    Task<AutoTransitionResult> ExecuteAutomaticAndScheduledTransitionsAsync(
        Definitions.Workflow workflow,
        Instance instance,
        ScriptContext scriptContext,
        CancellationToken cancellationToken = default);
}
