namespace BBT.Workflow.Infrastructure.Dapr.Metadata;

/// <summary>
/// Represents a Dapr component's information retrieved from the Dapr metadata endpoint.
/// </summary>
public sealed class DaprComponentInfo
{
    /// <summary>
    /// Gets the name of the Dapr component.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the Dapr component (e.g., "bindings.http", "bindings.mqtt").
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the version of the Dapr component, if specified.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the component's metadata key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprComponentInfo"/> class.
    /// </summary>
    /// <param name="name">The component name.</param>
    /// <param name="type">The component type.</param>
    /// <param name="version">The component version.</param>
    /// <param name="metadata">The component metadata.</param>
    public DaprComponentInfo(
        string name,
        string type,
        string? version,
        IReadOnlyDictionary<string, string?> metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Version = version;
        Metadata = metadata ?? new Dictionary<string, string?>();
    }

    /// <summary>
    /// Gets a metadata value by key.
    /// </summary>
    /// <param name="key">The metadata key to look up.</param>
    /// <returns>The metadata value if found; otherwise, null.</returns>
    public string? GetMetadata(string key)
        => Metadata.TryGetValue(key, out var v) ? v : null;
}

