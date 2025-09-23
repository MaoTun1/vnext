using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Invokers;

internal sealed class TransitionInvoker(IWorkflowExecutionService workflowExecutionService) : ITransitionInvoker
{
    public Task<InstanceServiceResponse<TransitionOutput>> InvokeAsync(Guid instanceId, string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default) =>
        workflowExecutionService.ExecuteTransitionAsync(instanceId, transitionKey, input, cancellationToken);
}