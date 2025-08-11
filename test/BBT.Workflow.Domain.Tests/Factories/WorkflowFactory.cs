namespace BBT.Workflow;

public static class WorkflowFactory
{
    public static Definitions.Workflow CreateDefault(string key = "test-flow", string domain = "test",
        string version = "1.0.0", string type = "F")
    {
        var item = Definitions.Workflow.Create();
        item.SetReference(new Reference(key, domain, "sys-flows", version));
        item.SetType(type);
        return item;
    }
}