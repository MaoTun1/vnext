using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution;

namespace BBT.Workflow.Instances;

public sealed class TransitionInput(
    string domain,
    string workflow,
    string? version,
    JsonElement? data = null,
    bool sync = false)
{
    public string Domain { get; set; } = domain;
    public string Workflow { get; set; } = workflow;
    public string? Version { get; set; } = version;
    public JsonElement? Data { get; set; } = data;
    public Dictionary<string, string?> Headers { get; set; } = new();
    public Dictionary<string, string?> RouteValues { get; set; } = new();
    public bool Sync { get; set; } = sync;

    /// <summary>
    /// Creates a WorkflowExecutionContext from this TransitionInput for manual transition execution.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier</param>
    /// <param name="transitionKey">The transition key to execute</param>
    /// <returns>A new WorkflowExecutionContext instance</returns>
    public WorkflowExecutionContext ToExecutionContext(string instanceId, string transitionKey)
    {
        return new WorkflowExecutionContext
        {
            Domain = Domain,
            InstanceId = instanceId,
            WorkflowKey = Workflow,
            WorkflowVersion = Version,
            TransitionKey = transitionKey,
            TriggerType = TriggerType.Manual, // TransitionInput always represents manual triggers
            Mode = Sync ? ExecMode.Sync : ExecMode.Async,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAt = DateTimeOffset.UtcNow,
            Headers = Headers,
            RouteValues = RouteValues,
            Data = Data,
            IsReentry = false // Manual transitions are never re-entry
        };
    }
}

public sealed class TransitionOutput
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Instance status (Active, Busy, Completed, etc.)
    /// </summary>
    public InstanceStatus? Status { get; set; }
}