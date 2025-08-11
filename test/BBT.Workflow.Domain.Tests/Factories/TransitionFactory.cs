using BBT.Workflow.Definitions;

namespace BBT.Workflow;

public static class TransitionFactory
{
    public static Transition CreateDefault(
        string key = "test-transition",
        string? fromState = "from-state",
        string toState = "to-state",
        TriggerType triggerType = TriggerType.Manual)
    {
        return Transition.Create(key, fromState, toState, triggerType, VersionStrategy.IncreaseMinor.Code);
    }
}