namespace BBT.Workflow.Execution.Pipeline;

/// <summary>
/// Represents a single step in the transition execution pipeline.
/// Each step performs a specific operation during the transition lifecycle.
/// </summary>
public interface ITransitionStep
{
    /// <summary>
    /// Gets the execution order of this step in the pipeline.
    /// Steps are executed in ascending order of their Order value.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Executes this step of the transition pipeline.
    /// </summary>
    /// <param name="context">The transition execution context containing all necessary data.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}