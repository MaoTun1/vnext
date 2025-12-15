using BBT.Aether.Results;

namespace BBT.Workflow.Instances;

/// <summary>
/// Service for handling instance cancellation operations.
/// Processes job cleanup when an instance is canceled.
/// </summary>
public interface IInstanceCancellationService
{
    /// <summary>
    /// Processes cancellation for an instance by cleaning up active jobs.
    /// </summary>
    /// <param name="instanceId">The ID of the canceled instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure of the cancellation processing.</returns>
    Task<Result> ProcessCancellationAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);
}

