using BBT.Workflow.Execution.Pipeline;

namespace BBT.Workflow.Execution.Planner;

public sealed class DefaultPlanner : IPipelinePlanner
{
    // This scheduler simply selects/sorts the list; the “wait/dispatch” semantics are inside the steps.
    public IReadOnlyList<ITransitionStep> Build(TransitionExecutionContext context,
        IEnumerable<ITransitionStep> allSteps)
    {
        var ordered = allSteps.OrderBy(s => s.Order).ToList();

        // 1) ResumeFrom start
        var startOrder = context.Directives.ConsumeResumeFrom(); // one-time
        if (startOrder.HasValue)
            ordered = ordered.Where(s => s.Order >= startOrder.Value).ToList();

        // 2) Subflow terminal short circuit (until Finalize)
        if (context.Directives.TerminalReached)
        {
            var maxOrder = LifecycleOrder.Finalize;
            ordered = ordered.Where(s => s.Order <= maxOrder).ToList();
        }

        // 3) Epilogue policy
        if (context.Directives.Epilogue == EpilogueMode.Skip)
        {
            ordered = ordered
                .Where(s => s.Order != LifecycleOrder.Schedule &&
                            s.Order != LifecycleOrder.Auto)
                .ToList();
        }
        // DispatchOnly: Stay within the Schedule/Auto plan; steps will dispatch + stop themselves.

        // 4) Example: If ResumeFrom=Schedule then BUSY→READY for cleaning
        // Make sure ClearBusyOnResumeStep (Schedule-1) is in the schedule
        if (startOrder == LifecycleOrder.Schedule)
        {
            var hasClearBusy = ordered.Any(s => s.Order == LifecycleOrder.Schedule - 1);
            if (!hasClearBusy)
            {
                // note: You can add it if the step is registered in the runtime, otherwise this part is unnecessary.
            }
        }

        return ordered;
    }
}