namespace BBT.Workflow.Execution.ReEntry;

/// <summary>
/// Dispatcher for handling re-entry transition execution in new scopes.
/// Manages automatic and scheduled transitions that need to be executed separately.
/// </summary>
public interface IReentryDispatcher
{
    /// <summary>
    /// Dispatches an automatic transition for execution.
    /// May execute inline or enqueue as a background job based on configuration.
    /// </summary>
    /// <param name="command">The re-entry command containing execution details.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<ReentryOutcome> DispatchAutoAsync(ReentryCommand command, CancellationToken cancellationToken);
}
