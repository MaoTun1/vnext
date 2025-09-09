using System.Text.Json;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

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
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string?> RouteValues { get; set; } = new();
    public bool Sync { get; set; } = sync;
    
    /// <summary>
    /// Execution context - who is executing this transition
    /// </summary>
    public WorkflowExecutionContext ExecutionContext { get; init; } = WorkflowExecutionContext.User;
}

public sealed class TransitionOutput
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Instance status (Active, Busy, Completed, etc.)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Current state of the instance
    /// </summary>
    public string? CurrentState { get; set; }

    /// <summary>
    /// Active SubFlow/SubProcess correlations for this instance
    /// </summary>
    public List<InstanceCorrelationInfo> ActiveCorrelations { get; set; } = [];
}