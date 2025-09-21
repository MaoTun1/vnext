using BBT.Aether.Application;

namespace BBT.Workflow.Instances;

public interface IInstanceCommandAppService : IApplicationService
{
    Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default);

    Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a start instance operation for background jobs.
    /// This method is specifically designed for background job execution and handles pre-created instances.
    /// </summary>
    Task<InstanceServiceResponse<StartInstanceOutput>> ExecuteBackgroundStartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a manual transition operation for background jobs.
    /// This method is specifically designed for background job execution and handles pre-reserved instances.
    /// </summary>
    Task<InstanceServiceResponse<TransitionOutput>> ExecuteBackgroundTransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
} 