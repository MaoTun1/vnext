using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

public class SubflowCompletionException(string domain, string flow, string instance, string errorCode, string reason) : UserFriendlyException(
    code: WorkflowErrorCodes.SubflowCompletionFailed,
    message: $"Subflow completion failed for domain: {domain}, flow: {flow}, instance: {instance}, error code: {errorCode}, reason: {reason}");