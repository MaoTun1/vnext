using BBT.Workflow.Definitions;

namespace BBT.Workflow;

public static class StateFactory
{
    public static State CreateDefault(string key = "test-state", StateType type = StateType.Initial, StateSubType subType = StateSubType.None)
    {
        var state = State.Create(key, type, subType, VersionStrategy.IncreaseMinor.Code);
        return state;
    }
}