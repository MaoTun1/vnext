using BBT.Workflow.Definitions;

namespace BBT.Workflow;

public static class StateFactory
{
    public static State CreateDefault(string key = "test-state", StateType type = StateType.Initial)
    {
        var state = State.Create(key, type, VersionStrategy.IncreaseMinor.Code);
        return state;
    }
}