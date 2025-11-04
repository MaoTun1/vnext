using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

/// <summary>
/// SignalR Task Definition
/// </summary>
public sealed class NotificationTask : WorkflowTask
{
    private NotificationTask()
    {
    }

    [JsonConstructor]
    private NotificationTask(
        JsonElement config) : base(config)
    {
         Type = ((int)TaskType.Notification).ToString();
    }



    /// <summary>
    /// Additional metadata for notification sending (e.g., componentName, topic, headers)
    /// </summary>
    public JsonElement? Metadata { get; private set; }

    /// <summary>
    /// Internal property setters for object pooling
    /// </summary>
    internal void SetMetadataInternal(JsonElement? metadata) => Metadata = metadata;

    protected override void Configure(JsonElement config)
    {
        base.Configure(config);

        if (config.TryGetProperty("metadata", out var metadataElement))
        {
            var metadataRaw = metadataElement.GetRawText();
            Metadata = string.IsNullOrWhiteSpace(metadataRaw) ? null : metadataElement;
        }
    }

    public static NotificationTask Create(
        JsonElement config)
    {
        return new NotificationTask(config);
    }

    /// <summary>
    /// Creates a deep copy of the current SignalRTask instance.
    /// </summary>
    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }

    /// <summary>
    /// Creates a typed deep copy of the current SignalRTask instance.
    /// </summary>
    public NotificationTask CloneTyped()
    {
        var cloned = new NotificationTask();
        CopyBaseTo(cloned);

        cloned.Metadata = Metadata;

        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(NotificationTask source)
    {
        source.CopyBaseToInternal(this);
        SetMetadataInternal(source.Metadata);
    }

    /// <summary>
    /// Resets the task instance to a clean state for object pooling
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        Metadata = null;
    }

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static NotificationTask CreateEmpty()
    {
        return new NotificationTask();
    }
}