using System.ComponentModel.DataAnnotations;
using BBT.Workflow.Definitions;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Tasks;

public class TaskExecutionRequestInput
{
    [Required] public required OnExecuteTaskInput OnExecuteTask { get; set; }

    [Required] public required TaskTrigger TaskTrigger { get; set; }

    public Guid? InstanceTransitionId { get; set; }
    public TaskScriptContextModel Context { get; set; }
}

public class TaskScriptContextModel
{
    /// <summary>
    /// Instance ID
    /// </summary>
    public Guid InstanceId { get; set; }
    
    /// <summary>
    /// Transition key
    /// </summary>
    public string? TransitionKey { get; set; }
    
    /// <summary>
    /// Workflow definition
    /// </summary>
    public ReferenceInput Workflow { get; set; }

    /// <summary>
    /// Request body data
    /// </summary>
    public object? Body { get; set; }

    /// <summary>
    /// Request headers
    /// </summary>
    public Dictionary<string, string?>? Headers { get; set; }

    /// <summary>
    /// Route values
    /// </summary>
    public Dictionary<string, object?>? RouteValues { get; set; }

    /// <summary>
    /// Task responses
    /// </summary>
    public Dictionary<string, object?> TaskResponse { get; set; } = new();

    /// <summary>
    /// Metadata
    /// </summary>
    public Dictionary<string, object>? MetaData { get; set; } = new();

    /// <summary>
    /// Definitions
    /// </summary>
    public Dictionary<string, object>? Definitions { get; set; } = new();
}