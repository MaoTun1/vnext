using System.Text.Json;
using System.Text.Json.Serialization;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Start Task Definition - Creates a new workflow instance
/// </summary>
public sealed class StartTask : WorkflowTask
{
    private StartTask()
    {
    }

    [JsonConstructor]
    private StartTask(JsonElement config) : base(config)
    {
        Type = ((int)TaskType.StartTrigger).ToString();
    }

    /// <summary>
    /// Domain of the target workflow
    /// </summary>
    public string TriggerDomain { get; private set; } = string.Empty;

    /// <summary>
    /// Flow name of the target workflow
    /// </summary>
    public string TriggerFlow { get; private set; } = string.Empty;

    /// <summary>
    /// Body data to send with the start request
    /// </summary>
    public JsonElement? Body { get; private set; }

    public void SetBody(dynamic body)
    {
        Body = JsonSerializer.SerializeToElement(body);
    }

    public void SetDomain(string domain)
    {
        TriggerDomain = domain;
    }

    public void SetFlow(string flow)
    {
        TriggerFlow = flow;
    }

    /// <summary>
    /// Internal property setters for object pooling
    /// </summary>
    internal void SetTriggerDomainInternal(string triggerDomain) => TriggerDomain = triggerDomain;
    internal void SetTriggerFlowInternal(string triggerFlow) => TriggerFlow = triggerFlow;
    internal void SetBodyInternal(JsonElement? body) => Body = body;

    protected override void Configure(JsonElement config)
    {
        base.Configure(config);

        if (config.TryGetProperty("domain", out var triggerDomainElement))
            TriggerDomain = triggerDomainElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerDomain))
            throw new ArgumentException("Property 'domain' is required for StartTask.", nameof(config));

        if (config.TryGetProperty("flow", out var triggerFlowElement))
            TriggerFlow = triggerFlowElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerFlow))
            throw new ArgumentException("Property 'flow' is required for StartTask.", nameof(config));

        if (config.TryGetProperty("body", out var bodyElement))
        {
            var body = bodyElement.GetRawText();
            Body = string.IsNullOrWhiteSpace(body) ? null : bodyElement;
        }
    }

    public static StartTask Create(JsonElement config)
    {
        return new StartTask(config);
    }

    /// <summary>
    /// Creates a deep copy of the current StartTask instance.
    /// </summary>
    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }

    /// <summary>
    /// Creates a typed deep copy of the current StartTask instance.
    /// </summary>
    public StartTask CloneTyped()
    {
        var cloned = new StartTask();
        CopyBaseTo(cloned);

        cloned.TriggerDomain = TriggerDomain;
        cloned.TriggerFlow = TriggerFlow;
        cloned.Body = Body;

        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(StartTask source)
    {
        source.CopyBaseToInternal(this);
        SetTriggerDomainInternal(source.TriggerDomain);
        SetTriggerFlowInternal(source.TriggerFlow);
        SetBodyInternal(source.Body);
    }

    /// <summary>
    /// Resets the task instance to a clean state for object pooling
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        TriggerDomain = string.Empty;
        TriggerFlow = string.Empty;
        Body = null;
    }

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static StartTask CreateEmpty()
    {
        return new StartTask();
    }
}

