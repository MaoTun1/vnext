using System;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Application.Tests.Execution.Transitions.Handlers;

/// <summary>
/// Helper methods for handler tests
/// </summary>
internal static class HandlerTestHelpers
{
    public static Instance CreateMockInstance(Guid instanceId, string flow)
    {
        var instance = Instance.Create(instanceId, flow);
        
        // Set current state using reflection since it's private setter
        typeof(Instance)
            .GetProperty(nameof(Instance.CurrentState))!
            .SetValue(instance, "state1");

        return instance;
    }

    public static Definitions.Workflow CreateMockWorkflow(string key, string domain)
    {
        var json = """
        {
            "type": "F",
            "timeout": null,
            "labels": [],
            "functions": [],
            "features": [],
            "states": [
                {
                    "key": "state1",
                    "type": "P",
                    "transitions": []
                }
            ],
            "sharedTransitions": [],
            "extensions": [],
            "startTransition": {"key": "start", "from": null, "target": "state1", "triggerType": "Manual", "versionStrategy": "Patch", "labels": [], "onExecutionTasks": [], "view": null}
        }
        """;

        var options = new System.Text.Json.JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var workflow = System.Text.Json.JsonSerializer.Deserialize<Definitions.Workflow>(json, options)!;

        workflow.SetReference(new Reference(key, domain, "sys-flows", "1.0.0"));
        return workflow;
    }

    public static Transition CreateMockTransition(string key, string target, TriggerType triggerType)
    {
        return Transition.Create(key, null, target, triggerType, "Patch");
    }
}

