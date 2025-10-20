using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.ReEntry;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Execution;

/// <summary>
/// Input data for workflow execution operations.
/// Contains all necessary information to execute a transition.
/// </summary>
public sealed class WorkflowExecutionContext
{
    /// <summary>Gets or sets the domain/tenant identifier.</summary>
    public string Domain { get; set; } = default!;
    
    /// <summary>Gets or sets the workflow instance identifier.</summary>
    public Guid InstanceId { get; set; }
    
    /// <summary>Gets or sets the workflow key.</summary>
    public string WorkflowKey { get; set; } = default!;
    
    /// <summary>Gets or sets the workflow version (optional, uses latest if not specified).</summary>
    public string? WorkflowVersion { get; set; }
    
    /// <summary>Gets or sets the transition key to execute.</summary>
    public string TransitionKey { get; set; } = default!;
    
    /// <summary>Gets or sets the trigger type for this execution.</summary>
    public TriggerType TriggerType { get; set; }
    
    /// <summary>Gets or sets the execution mode (sync/async).</summary>
    public ExecMode Mode { get; set; } = ExecMode.Sync;
    
    /// <summary> Get or sets the execution actor (default: User) </summary>
    public ExecutionActor Actor { get; set; } = ExecutionActor.User;
    
    /// <summary>Gets or sets the correlation identifier.</summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>Gets or sets the causation identifier.</summary>
    public string? CausationId { get; set; }
    
    /// <summary>Gets or sets the timestamp when this execution was requested.</summary>
    public DateTimeOffset? RequestedAt { get; set; }
    
    /// <summary>Gets or sets the request headers.</summary>
    public Dictionary<string, string?> Headers { get; set; } = new();
    
    /// <summary>Gets or sets the execution context information for re-entry scenarios.</summary>
    public ExecutionInfo? Execution { get; set; }
    
    /// <summary>Gets or sets whether this is a re-entry execution.</summary>
    public bool IsReentry { get; set; }
    
    /// <summary>Gets or sets the transition data payload.</summary>
    public JsonElement? Data { get; set; }
    
    /// <summary>Gets or sets the route values from the HTTP request.</summary>
    public Dictionary<string, string?> RouteValues { get; set; } = new();

    /// <summary>
    /// Creates a WorkflowExecutionInput from a ReentryCommand.
    /// </summary>
    /// <param name="command">The re-entry command to convert.</param>
    /// <returns>A new WorkflowExecutionInput instance.</returns>
    public static WorkflowExecutionContext From(ReentryCommand command) => new()
    {
        Domain = command.Domain,
        InstanceId = command.InstanceId,
        WorkflowKey = command.WorkflowKey,
        TransitionKey = command.TransitionKey,
        TriggerType = command.TriggerType,
        Mode = ExecMode.Sync, // Re-entry is always sync
        CorrelationId = Guid.NewGuid().ToString("N"),
        CausationId = command.ExecutionChainId,
        RequestedAt = DateTimeOffset.UtcNow,
        Headers = command.Headers?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new(),
        Execution = new ExecutionInfo
        {
            ExecutionChainId = command.ExecutionChainId ?? Guid.NewGuid().ToString("N"),
            ChainDepth = command.ChainDepth,
            ResumeFrom = null
        },
        IsReentry = true,
        Actor = command.Actor
    };
}