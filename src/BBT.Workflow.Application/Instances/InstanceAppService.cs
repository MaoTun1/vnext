using BBT.Aether.Application.Services;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Legacy service that combines command and query operations for backward compatibility.
/// Consider using IInstanceCommandAppService and IInstanceQueryAppService separately for better separation of concerns.
/// </summary>
public sealed class InstanceAppService(
    IServiceProvider serviceProvider,
    IInstanceCommandAppService commandAppService,
    IInstanceQueryAppService queryAppService)
    : ApplicationService(serviceProvider), IInstanceAppService
{
    public async Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        return await commandAppService.StartAsync(input, cancellationToken);
    }

    public async Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        return await commandAppService.TransitionAsync(instanceId, transitionKey, input, cancellationToken);
    }

    public async Task<InstanceServiceResult<GetInstanceOutput>> GetInstanceAsync(
        GetInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        return await queryAppService.GetInstanceAsync(input, cancellationToken);
    }

    public async Task<InstanceServiceResponse<PaginationResult<GetInstanceOutput>>> GetInstanceListAsync(
        GetInstanceListInput input,
        CancellationToken cancellationToken = default)
    {
        return await queryAppService.GetInstanceListAsync(input, cancellationToken);
    }

    public async Task<InstanceServiceResponse<GetInstanceHistoryOutput>> GetInstanceHistoryAsync(
        GetInstanceHistoryInput input,
        CancellationToken cancellationToken = default)
    {
        return await queryAppService.GetInstanceHistoryAsync(input, cancellationToken);
    }

    public  async Task<InstanceServiceResponse<GetAvailableTransitionOutput>> GetAvailableTransitionsAsync(GetAvailableTransitionInput input, CancellationToken cancellationToken = default)
    {
        return await queryAppService.GetAvailableTransitionsAsync(input, cancellationToken);
    }
}