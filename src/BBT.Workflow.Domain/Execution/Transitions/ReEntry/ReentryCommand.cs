using BBT.Workflow.Definitions;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Execution.ReEntry;

/// <summary>
/// Command object containing all necessary information for re-entry transition execution.
/// Used for automatic and scheduled transitions that need to be executed in a new scope.
/// </summary>
public sealed record ReentryCommand(
    Guid InstanceId,
    string Domain,
    string WorkflowKey,
    string TransitionKey,
    TriggerType TriggerType,
    ExecutionActor Actor = ExecutionActor.System,
    string? ExecutionChainId = null,
    int ChainDepth = 0,
    bool PreferInline = false,
    IReadOnlyDictionary<string, string?>? Headers = null)
{
    /// <summary>
    /// Creates a ReentryCommand for automatic transitions.
    /// </summary>
    public static ReentryCommand ForAutomatic(
        Guid instanceId,
        string domain,
        string workflowKey,
        string transitionKey,
        string executionChainId,
        int chainDepth,
        IReadOnlyDictionary<string, string?>? headers = null) =>
        new(instanceId, domain, workflowKey, transitionKey, TriggerType.Automatic,
            ExecutionActor.System, executionChainId, chainDepth, true, headers);
    
    /// <summary>
    /// Creates a ReentryCommand for scheduled transitions.
    /// </summary>
    public static ReentryCommand ForScheduled(
        Guid instanceId,
        string domain,
        string workflowKey,
        string transitionKey,
        string executionChainId,
        int chainDepth,
        IReadOnlyDictionary<string, string?>? headers = null) =>
        new(instanceId, domain, workflowKey, transitionKey, TriggerType.Scheduled,
            ExecutionActor.System, executionChainId, chainDepth, false, headers);
}
