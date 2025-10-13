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
    
    /// <summary>
    /// Retrieves only the instance data (attributes) with optional ETag support
    /// </summary>
    Task<InstanceServiceResult<GetInstanceDataOutput>> GetInstanceDataAsync(
        GetInstanceDataInput input,
        CancellationToken cancellationToken = default);
    
    
    /// <summary>
    /// Retrieves available transitions with system view information including href links
    /// </summary>
    Task<InstanceServiceResponse<GetAvailableSysGetViewOutput>> GetAvailableSysGetViewAsync(
        GetAvailableSysGetViewInput input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves transition items for an instance
    /// </summary>
    Task<InstanceServiceResponse<GetTransitionItemsOutput>> GetTransitionItemsAsync(
        GetTransitionItemsInput input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves active correlations for an instance
    /// </summary>
    Task<InstanceServiceResponse<GetActiveCorrelationsOutput>> GetActiveCorrelationsAsync(
        GetActiveCorrelationsInput input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the view for an instance based on current state and available transitions
    /// </summary>
    Task<InstanceServiceResponse<GetViewOutput>> GetViewAsync(
        GetViewInput input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the complete state information for an instance including data href, view, state, status, correlations, transitions and ETag
    /// </summary>
    Task<InstanceServiceResponse<GetInstanceStateOutput>> GetInstanceStateAsync(
        GetInstanceStateInput input,
        CancellationToken cancellationToken = default);
} 