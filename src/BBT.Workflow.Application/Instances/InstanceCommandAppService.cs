using BBT.Aether.Application.Services;
using BBT.Workflow.Execution.Services;

namespace BBT.Workflow.Instances;

public sealed class InstanceCommandAppService(
    IServiceProvider serviceProvider,
    IWorkflowExecutionService workflowExecutionService)
    : ApplicationService(serviceProvider), IInstanceCommandAppService
{
    /// <inheritdoc />
    public async Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        return await workflowExecutionService.ExecuteStartAsync(input, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        return await workflowExecutionService.ExecuteTransitionAsync(instanceId, transitionKey, input, cancellationToken);
    }
}