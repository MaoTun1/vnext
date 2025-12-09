namespace BBT.Workflow.Infrastructure.Dapr.Metadata;

/// <summary>
/// Provides access to Dapr metadata, including component information.
/// The metadata is cached after first retrieval to avoid repeated network calls.
/// </summary>
public interface IDaprMetadataProvider
{
    /// <summary>
    /// Gets all Dapr components from the metadata endpoint.
    /// Returns cached data after the first successful retrieval.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of Dapr component information.</returns>
    Task<IReadOnlyList<DaprComponentInfo>> GetComponentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-loads the Dapr metadata during application startup.
    /// Call this method to ensure metadata is available before first use.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WarmUpAsync(CancellationToken cancellationToken = default);
}

