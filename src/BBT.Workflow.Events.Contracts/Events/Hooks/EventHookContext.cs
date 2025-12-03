namespace BBT.Workflow.Events.Hooks;

/// <summary>
/// Represents the context provided to an event hook during execution.
/// Contains all necessary information about the event being published.
/// </summary>
/// <param name="EventData">The actual event object being published.</param>
/// <param name="EventType">The fully qualified type name of the event.</param>
/// <param name="Topic">The topic/subject where the event will be published.</param>
/// <param name="Metadata">
/// Dictionary of metadata associated with the event. Hooks can read existing metadata
/// and add new metadata through the <see cref="EventHookResult"/>.
/// </param>
/// <remarks>
/// This is an immutable record that provides read-only access to event information.
/// To add or modify metadata, return an <see cref="EventHookResult"/> with extra metadata.
/// </remarks>
public sealed record EventHookContext(
    object EventData,
    string EventType,
    string Topic,
    IDictionary<string, string> Metadata
);

