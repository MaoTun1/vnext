namespace BBT.Workflow.Tasks;

/// <summary>
/// Trace context for distributed tracing of task execution.
/// Contains information needed for observability and correlation.
/// </summary>
public sealed record TaskTraceContext
{
    /// <summary>
    /// Instance ID being processed.
    /// </summary>
    public Guid InstanceId { get; init; }
    
    /// <summary>
    /// Domain of the workflow.
    /// </summary>
    public string Domain { get; init; } = string.Empty;
    
    /// <summary>
    /// Key of the workflow.
    /// </summary>
    public string WorkflowKey { get; init; } = string.Empty;
    
    /// <summary>
    /// Version of the workflow.
    /// </summary>
    public string WorkflowVersion { get; init; } = string.Empty;
    
    /// <summary>
    /// Optional correlation ID for cross-service tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// Creates a trace context from workflow and instance information.
    /// </summary>
    public static TaskTraceContext Create(
        Guid instanceId,
        string domain,
        string workflowKey,
        string workflowVersion,
        string? correlationId = null) => new()
    {
        InstanceId = instanceId,
        Domain = domain,
        WorkflowKey = workflowKey,
        WorkflowVersion = workflowVersion,
        CorrelationId = correlationId
    };
}

