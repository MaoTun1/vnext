using BBT.Aether.Application;

namespace BBT.Workflow.Instances;

public interface IInstanceQueryAppService : IApplicationService
{
    /// <summary>
    /// Retrieves a single instance with optional extensions for data enrichment
    /// </summary>
    Task<InstanceServiceResult<GetInstanceOutput>> GetInstanceAsync(
        GetInstanceInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of instances with optional extensions
    /// </summary>
    Task<InstanceServiceResponse<Definitions.PaginationResult<GetInstanceOutput>>> GetInstanceListAsync(
        GetInstanceListInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete history of an instance (all data transitions)
    /// </summary>
    Task<InstanceServiceResponse<GetInstanceHistoryOutput>> GetInstanceHistoryAsync(
        GetInstanceHistoryInput input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the available transitions of an instance
    /// </summary>
    Task<InstanceServiceResponse<GetAvailableTransitionOutput>> GetAvailableTransitionsAsync(
        GetAvailableTransitionInput input,
        CancellationToken cancellationToken = default);
} 