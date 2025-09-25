using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

public class NotFoundTransitionException(string transitionKey): UserFriendlyException(code: WorkflowErrorCodes.NotFoundTransition, message: $"Transition \"{transitionKey}\" not found'");

public class TransitionRuleFailedException(string transitionKey, string reason) : UserFriendlyException(
    code: WorkflowErrorCodes.TransitionRuleFailed, 
    message: $"Transition \"{transitionKey}\" rule evaluation failed: {reason}");

public class AutoTransitionFailedException(Guid instanceId, string workflow) : UserFriendlyException(
    code: WorkflowErrorCodes.AutoTransitionFailed,
    message: $"No automatic transition succeeded for InstanceId={instanceId} in Workflow={workflow}");
