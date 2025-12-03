using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when a cancel transition is requested but the workflow does not have a cancel configuration.
/// </summary>
public class CancelNotConfiguredForWorkflowException(string workflowKey) : UserFriendlyException(
    code: WorkflowErrorCodes.CancelNotConfiguredForWorkflow,
    message: $"This workflow \"{workflowKey}\" does not define a cancel configuration");

