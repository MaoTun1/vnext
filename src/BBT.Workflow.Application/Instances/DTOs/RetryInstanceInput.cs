using BBT.Workflow.Definitions;
using BBT.Workflow.Execution;

namespace BBT.Workflow.Instances;

/// <summary>
/// Input parameters for retrying a faulted workflow instance.
/// </summary>
public sealed class RetryInstanceInput
{
    /// <summary>
    /// The domain/tenant identifier.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// The workflow key.
    /// </summary>
    public required string Workflow { get; init; }

    /// <summary>
    /// The workflow version. If not specified, uses the version from the incomplete transition.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// The instance identifier (ID or key).
    /// </summary>
    public required string Instance { get; init; }

    /// <summary>
    /// Whether to execute synchronously.
    /// </summary>
    public bool Sync { get; init; }

    /// <summary>
    /// Optional data payload for the retry execution.
    /// </summary>
    public TransitionDataInput? Data { get; init; }

    /// <summary>
    /// Request headers for context propagation.
    /// </summary>
    public Dictionary<string, string?> Headers { get; init; } = new();

    /// <summary>
    /// Route values from the HTTP request.
    /// </summary>
    public Dictionary<string, string?> RouteValues { get; init; } = new();

    /// <summary>
    /// Creates a WorkflowExecutionContext for retry execution.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier.</param>
    /// <param name="transitionKey">The transition key to retry.</param>
    /// <param name="version">The workflow version.</param>
    /// <param name="completedTaskIds">IDs of tasks that completed successfully and should be bypassed.</param>
    /// <returns>A new WorkflowExecutionContext configured for retry.</returns>
    public WorkflowExecutionContext ToExecutionContext(
        Guid instanceId,
        string transitionKey,
        string? version,
        IEnumerable<string>? completedTaskIds = null)
    {
        return new WorkflowExecutionContext
        {
            Domain = Domain,
            InstanceId = instanceId.ToString(),
            WorkflowKey = Workflow,
            WorkflowVersion = version ?? Version,
            TransitionKey = transitionKey,
            TriggerType = TriggerType.Manual, // Retry is always manual
            Mode = Sync ? ExecMode.Sync : ExecMode.Async,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAt = DateTimeOffset.UtcNow,
            Headers = Headers,
            RouteValues = RouteValues,
            IsReentry = true, // Retry is a re-entry scenario
            Execution = new ExecutionInfo
            {
                ExecutionChainId = Guid.NewGuid().ToString("N"),
                ChainDepth = 0
            }
        };
    }
}
