using System.Threading.Tasks;
using BBT.Workflow.Scripting;
using BBT.Workflow.Definitions;

public class MockMapping : IMapping
{
    public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
    {
        var httpTask = (task as HttpTask)!;
        httpTask.Url = "https://httpbin.org/post/" + context.Transition.Key;
        httpTask.Method = "POST";
        return Task.FromResult(new ScriptResponse
        {
            Data = "Hello Input",
            Headers = null
        });
    }

    public Task<ScriptResponse> OutputHandler(ScriptContext context)
    {
        return Task.FromResult(new ScriptResponse
        {
            Data = "Hello Output",
            Headers = null
        });
    }
}