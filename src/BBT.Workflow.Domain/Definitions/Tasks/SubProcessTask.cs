using System.Text.Json;
using System.Text.Json.Serialization;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Definitions;

/// <summary>
/// SubProcess Task Definition - Triggers transitions on correlated SubFlow instances
/// </summary>
public sealed class SubProcessTask : WorkflowTask
{
    private SubProcessTask()
    {
    }

    [JsonConstructor]
    private SubProcessTask(JsonElement config) : base(config)
    {
        Type = ((int)TaskType.SubProcess).ToString();
    }

    /// <summary>
    /// Domain of the target workflow
    /// </summary>
    public string TriggerDomain { get; private set; } = string.Empty;

    /// <summary>
    /// Flow key of the target workflow
    /// </summary>
    public string TriggerKey { get; private set; } = string.Empty;

    /// <summary>
    /// SubFlow version (optional)
    /// </summary>
    public string? TriggerVersion { get; private set; }

    /// <summary>
    /// Body data to send with the subprocess request
    /// </summary>
    public JsonElement? Body { get; private set; }

    public void SetBody(dynamic body)
    {
        Body = JsonSerializer.SerializeToElement(body);
    }

    public void SetKey(string key)
    {
        TriggerKey = key;
    }

    public void SetDomain(string domain)
    {
        TriggerDomain = domain;
    }

    public void SetVersion(string version)
    {
        TriggerVersion = version;
    }

    /// <summary>
    /// Internal property setters for object pooling
    /// </summary>
    internal void SetTriggerDomainInternal(string triggerDomain) => TriggerDomain = triggerDomain;
    internal void SetTriggerKeyInternal(string triggerKey) => TriggerKey = triggerKey;
    internal void SetTriggerVersionInternal(string? triggerVersion) => TriggerVersion = triggerVersion;
    internal void SetBodyInternal(JsonElement? body) => Body = body;

    protected override void Configure(JsonElement config)
    {
        base.Configure(config);

        if (config.TryGetProperty("domain", out var triggerDomainElement))
            TriggerDomain = triggerDomainElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerDomain))
            throw new ArgumentException("Property 'domain' is required for SubProcessTask.", nameof(config));

        if (config.TryGetProperty("key", out var keyElement))
            TriggerKey = keyElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerKey))
            throw new ArgumentException("Property 'key' is required for SubProcessTask.", nameof(config));

        if (config.TryGetProperty("version", out var versionElement))
            TriggerVersion = versionElement.GetString();

        if (config.TryGetProperty("body", out var bodyElement))
        {
            var body = bodyElement.GetRawText();
            Body = string.IsNullOrWhiteSpace(body) ? null : bodyElement;
        }
    }

    public static SubProcessTask Create(JsonElement config)
    {
        return new SubProcessTask(config);
    }

    /// <summary>
    /// Creates a deep copy of the current SubProcessTask instance.
    /// </summary>
    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }

    /// <summary>
    /// Creates a typed deep copy of the current SubProcessTask instance.
    /// </summary>
    public SubProcessTask CloneTyped()
    {
        var cloned = new SubProcessTask();
        CopyBaseTo(cloned);

        cloned.TriggerDomain = TriggerDomain;
        cloned.TriggerKey = TriggerKey;
        cloned.TriggerVersion = TriggerVersion;
        cloned.Body = Body;

        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(SubProcessTask source)
    {
        source.CopyBaseToInternal(this);
        SetTriggerDomainInternal(source.TriggerDomain);
        SetTriggerKeyInternal(source.TriggerKey);
        SetTriggerVersionInternal(source.TriggerVersion);
        SetBodyInternal(source.Body);
    }

    /// <summary>
    /// Resets the task instance to a clean state for object pooling
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        TriggerDomain = string.Empty;
        TriggerKey = string.Empty;
        TriggerVersion = null;
        Body = null;
    }

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static SubProcessTask CreateEmpty()
    {
        return new SubProcessTask();
    }
}

