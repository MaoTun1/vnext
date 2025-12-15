using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Notification Task Definition for sending notifications through various channels
/// (SignalR, MQTT, Kafka, HTTP webhook, etc.)
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
    /// The notification message body/payload. Set by mapping during execution.
    /// </summary>
    public object? Body { get; private set; }

    /// <summary>
    /// The notification subject/title. Set by mapping during execution.
    /// </summary>
    public string? Subject { get; private set; }

    /// <summary>
    /// The recipients of the notification. Set by mapping during execution.
    /// Can be user IDs, email addresses, topic names, etc. depending on the binding type.
    /// </summary>
    public string[]? To { get; private set; }

    /// <summary>
    /// Additional metadata for notification sending (e.g., componentName, topic, headers).
    /// Configured in workflow definition.
    /// </summary>
    public JsonElement? Metadata { get; private set; }

    /// <summary>
    /// Sets the notification body/payload. Called by mapping during execution.
    /// </summary>
    /// <param name="body">The notification body object.</param>
    public void SetBody(object? body) => Body = body;

    /// <summary>
    /// Sets the notification subject/title. Called by mapping during execution.
    /// </summary>
    /// <param name="subject">The notification subject.</param>
    public void SetSubject(string? subject) => Subject = subject;

    /// <summary>
    /// Sets the notification recipients. Called by mapping during execution.
    /// </summary>
    /// <param name="to">The recipients array.</param>
    public void SetTo(string[]? to) => To = to;

    /// <summary>
    /// Sets a single recipient. Called by mapping during execution.
    /// </summary>
    /// <param name="to">The single recipient.</param>
    public void SetTo(string? to) => To = string.IsNullOrEmpty(to) ? null : new[] { to };

    /// <summary>
    /// Internal property setters for object pooling
    /// </summary>
    internal void SetMetadataInternal(JsonElement? metadata) => Metadata = metadata;
    internal void SetBodyInternal(object? body) => Body = body;
    internal void SetSubjectInternal(string? subject) => Subject = subject;
    internal void SetToInternal(string[]? to) => To = to;

    protected override void Configure(JsonElement config)
    {
        base.Configure(config);

        if (config.TryGetProperty("metadata", out var metadataElement))
        {
            var metadataRaw = metadataElement.GetRawText();
            Metadata = string.IsNullOrWhiteSpace(metadataRaw) ? null : metadataElement;
        }

        // Subject and To can be configured statically in workflow definition
        if (config.TryGetProperty("subject", out var subjectElement))
        {
            Subject = subjectElement.GetString();
        }

        if (config.TryGetProperty("to", out var toElement))
        {
            if (toElement.ValueKind == JsonValueKind.Array)
            {
                To = toElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray()!;
            }
            else if (toElement.ValueKind == JsonValueKind.String)
            {
                var toValue = toElement.GetString();
                To = string.IsNullOrEmpty(toValue) ? null : new[] { toValue };
            }
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
    /// Creates a typed deep copy of the current NotificationTask instance.
    /// </summary>
    public NotificationTask CloneTyped()
    {
        var cloned = new NotificationTask();
        CopyBaseTo(cloned);

        cloned.Metadata = Metadata;
        cloned.Body = Body;
        cloned.Subject = Subject;
        cloned.To = To;

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
        SetBodyInternal(source.Body);
        SetSubjectInternal(source.Subject);
        SetToInternal(source.To);
    }

    /// <summary>
    /// Resets the task instance to a clean state for object pooling
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        Metadata = null;
        Body = null;
        Subject = null;
        To = null;
    }

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static NotificationTask CreateEmpty()
    {
        return new NotificationTask();
    }
}