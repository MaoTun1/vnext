using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Invokers;

public interface ITransitionInvoker
{
    Task<InstanceServiceResponse<TransitionOutput>> InvokeAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
}