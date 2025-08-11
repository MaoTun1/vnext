namespace BBT.Workflow.Scripting;

public interface IConditionMapping
{
    Task<bool> Handler(ScriptContext context);
}