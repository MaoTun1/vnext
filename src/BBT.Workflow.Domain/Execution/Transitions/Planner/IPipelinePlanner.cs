using BBT.Workflow.Execution.Pipeline;

namespace BBT.Workflow.Execution.Planner;

public interface IPipelinePlanner
{
    IReadOnlyList<ITransitionStep> Build(TransitionExecutionContext context, IEnumerable<ITransitionStep> allSteps);
}