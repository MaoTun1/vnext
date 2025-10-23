using BBT.Workflow.Domain;

namespace BBT.Workflow.Execution;

/// <summary>
/// Provides functionality to refresh and rehydrate transition execution context data.
/// Used to reload instance and workflow data during pipeline execution.
/// </summary>
public interface IContextRefresher
{
    /// <summary>
    /// Refreshes the context by reloading instance, workflow, and state information.
    /// </summary>
    /// <param name="context">The transition execution context to refresh.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous refresh operation.</returns>
    Task<Result> RefreshAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}