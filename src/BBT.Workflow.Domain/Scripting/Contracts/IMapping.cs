using BBT.Workflow.Definitions;

namespace BBT.Workflow.Scripting;

public interface IMapping
{
    Task<ScriptResponse> InputHandler(
        WorkflowTask task,
        ScriptContext context);

    Task<ScriptResponse> OutputHandler(
        ScriptContext context);
}