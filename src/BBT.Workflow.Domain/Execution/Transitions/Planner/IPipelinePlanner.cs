using BBT.Workflow.Execution.Pipeline;

namespace BBT.Workflow.Execution.Planner;

/// <summary>
/// Plans and builds the pipeline execution sequence based on context and directives.
/// Determines which steps should be executed and in what order.
/// </summary>
public interface IPipelinePlanner
{
    /// <summary>
    /// Builds an ordered list of pipeline steps to execute based on context and directives.
    /// </summary>
    /// <param name="context">The transition execution context containing directives and state.</param>
    /// <param name="allSteps">All available pipeline steps to select from.</param>
    /// <returns>An ordered list of steps to execute.</returns>
    IReadOnlyList<ITransitionStep> Build(TransitionExecutionContext context, IEnumerable<ITransitionStep> allSteps);
}