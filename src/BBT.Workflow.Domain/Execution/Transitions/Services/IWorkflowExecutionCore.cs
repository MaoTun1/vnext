using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Core transition execution contract without UoW management.
/// The caller (TransitionRunner) is responsible for UoW lifecycle.
/// </summary>
public interface IWorkflowExecutionCore
{
    /// <summary>
    /// Executes a single transition's core logic without managing UoW.
    /// Returns both the transition output and directives snapshot for post-commit processing.
    /// </summary>
    /// <param name="context">The workflow execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A Result containing the core output with transition result and directives snapshot.</returns>
    Task<Result<TransitionCoreOutput>> ExecuteTransitionCoreAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}

