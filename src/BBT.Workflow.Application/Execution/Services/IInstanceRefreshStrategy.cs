using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Interface for instance refresh strategy operations.
/// </summary>
public interface IInstanceRefreshStrategy
{
    /// <summary>
    /// Gets the latest instance state with minimal database overhead.
    /// </summary>
    Task<Instance?> GetLatestInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an instance needs refresh based on its current state and context.
    /// </summary>
    bool ShouldRefreshInstance(Instance instance, bool afterAutoTransition = false);

    /// <summary>
    /// Conditionally refreshes an instance only if needed.
    /// </summary>
    Task<Instance> RefreshIfNeededAsync(Instance instance, bool afterAutoTransition = false, CancellationToken cancellationToken = default);
}