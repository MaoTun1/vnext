namespace BBT.Workflow.Execution.Notification;

/// <summary>
/// Contains resolved information about the notification binding component.
/// Includes the component name, type, classified kind, and metadata.
/// </summary>
public sealed class NotificationBindingInfo
{
    /// <summary>
    /// Gets the Dapr component name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the raw Dapr component type string (e.g., "bindings.http").
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the component version, if specified.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the classified binding kind based on the component type.
    /// </summary>
    public NotificationBindingKind Kind { get; }

    /// <summary>
    /// Gets the component metadata key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationBindingInfo"/> class.
    /// </summary>
    /// <param name="name">The component name.</param>
    /// <param name="type">The component type.</param>
    /// <param name="version">The component version.</param>
    /// <param name="kind">The classified binding kind.</param>
    /// <param name="metadata">The component metadata.</param>
    public NotificationBindingInfo(
        string name,
        string type,
        string? version,
        NotificationBindingKind kind,
        IReadOnlyDictionary<string, string?>? metadata = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Version = version;
        Kind = kind;
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

