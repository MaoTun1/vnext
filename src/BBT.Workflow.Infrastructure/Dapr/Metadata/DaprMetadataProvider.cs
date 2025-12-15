using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.Dapr.Metadata;

/// <summary>
/// Provides Dapr metadata by fetching component information from the Dapr sidecar
/// using the native DaprClient SDK.
/// Caches the metadata after first successful retrieval.
/// </summary>
public sealed class DaprMetadataProvider : IDaprMetadataProvider
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprMetadataProvider> _logger;

    private IReadOnlyList<DaprComponentInfo>? _cache;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprMetadataProvider"/> class.
    /// </summary>
    /// <param name="daprClient">The Dapr client.</param>
    /// <param name="logger">The logger.</param>
    public DaprMetadataProvider(
        DaprClient daprClient,
        ILogger<DaprMetadataProvider> logger)
    {
        _daprClient = daprClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DaprComponentInfo>> GetComponentsAsync(
        CancellationToken cancellationToken = default)
        => GetComponentsInternalAsync(forceReload: false, cancellationToken);

    /// <inheritdoc />
    public Task WarmUpAsync(CancellationToken cancellationToken = default)
        => GetComponentsInternalAsync(forceReload: false, cancellationToken);

    private async Task<IReadOnlyList<DaprComponentInfo>> GetComponentsInternalAsync(
        bool forceReload,
        CancellationToken cancellationToken)
    {
        // Fast path - return cached data
        if (!forceReload && _cache is not null)
            return _cache;

        lock (_lock)
        {
            if (!forceReload && _cache is not null)
                return _cache;
        }

        _logger.LogInformation("Loading Dapr metadata via DaprClient SDK");

        // Use DaprClient's native GetMetadataAsync method
        var metadata = await _daprClient.GetMetadataAsync(cancellationToken);

        var list = new List<DaprComponentInfo>();

        if (metadata.Components != null)
        {
            foreach (var component in metadata.Components)
            {
                // Convert component capabilities to metadata dictionary
                var metaDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                // Add capabilities as metadata if available
                if (component.Capabilities != null && component.Capabilities.Length > 0)
                {
                    metaDict["capabilities"] = string.Join(",", component.Capabilities);
                }

                list.Add(new DaprComponentInfo(
                    name: component.Name ?? string.Empty,
                    type: component.Type ?? string.Empty,
                    version: component.Version,
                    metadata: metaDict));
            }
        }

        lock (_lock)
        {
            _cache ??= list;
        }

        _logger.LogInformation(
            "Dapr metadata loaded via SDK. Components count: {Count}, AppId: {AppId}",
            _cache.Count,
            metadata.Id);

        return _cache;
    }
}
