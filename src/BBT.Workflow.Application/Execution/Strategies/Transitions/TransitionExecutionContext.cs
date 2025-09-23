using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Contains all necessary context data for transition execution strategies.
/// </summary>
public sealed record TransitionExecutionContext(
    Guid InstanceId,
    string TransitionKey,
    TransitionInput Input,
    Definitions.Workflow Workflow,
    Instance Instance,
    Transition Transition,
    IScriptContextBuilder ScriptContextBuilder)
{
    /// <summary>
    /// Gets a value indicating whether the execution should be synchronous.
    /// </summary>
    public bool IsSync => Input.Sync;
}
