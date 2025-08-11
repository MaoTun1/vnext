using System.Text.Json;
using BBT.Workflow.Definitions;

namespace BBT.Workflow;

public static class WorkflowTaskFactory
{
    public static HttpTask CreateHttpTask(string name = "mock-api")
    {
        var config = """
                      {
                        "url": "https://httpbin.org/get",
                        "method": "GET",
                        "headers": {
                            "User-Agent": "vNext/1.0"
                        },
                        "timeoutSeconds": 25
                      }
                     """;

        var task = HttpTask.Create(config.ToJsonElement());
        task.SetReference(new Reference(name, "test", "sys-tasks", "1.0.0"));
        return task;
    }
}