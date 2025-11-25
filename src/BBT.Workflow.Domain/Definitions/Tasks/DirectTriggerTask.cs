using System.Text.Json;
using System.Text.Json.Serialization;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Direct Trigger Task Definition - Executes a transition on a target workflow instance
/// </summary>
public sealed class DirectTriggerTask : WorkflowTask
{
    private DirectTriggerTask()
    {
    }

    [JsonConstructor]
    private DirectTriggerTask(JsonElement config) : base(config)
    {
        Type = ((int)TaskType.DirectTrigger).ToString();
    }

    /// <summary>
    /// Transition name to execute (required)
    /// </summary>
    public string? TransitionName { get; private set; }

    /// <summary>
    /// Domain of the target workflow
    /// </summary>
    public string TriggerDomain { get; private set; } = string.Empty;

    /// <summary>
    /// Flow name of the target workflow
    /// </summary>
    public string TriggerFlow { get; private set; } = string.Empty;

    /// <summary>
    /// Flow key of the target workflow (optional)
    /// </summary>
    public string? TriggerKey { get; private set; }

    /// <summary>
    /// InstanceId of the target workflow (optional)
    /// </summary>
    public string? TriggerInstanceId { get; private set; }

    /// <summary>
    /// Body data to send with the transition request
    /// </summary>
    public JsonElement? Body { get; private set; }

    public void SetBody(dynamic body)
    {
        Body = JsonSerializer.SerializeToElement(body);
    }

    public void SetInstance(string instanceId)
    {
        TriggerInstanceId = instanceId;
    }

    public void SetKey(string key)
    {
        TriggerKey = key;
    }

    public void SetDomain(string domain)
    {
        TriggerDomain = domain;
    }

    public void SetFlow(string flow)
    {
        TriggerFlow = flow;
    }

    public void SetTransitionName(string transitionName)
    {
        TransitionName = transitionName;
    }

    /// <summary>
    /// Internal property setters for object pooling
    /// </summary>
    internal void SetTransitionNameInternal(string? transitionName) => TransitionName = transitionName;
    internal void SetTriggerDomainInternal(string triggerDomain) => TriggerDomain = triggerDomain;
    internal void SetTriggerFlowInternal(string triggerFlow) => TriggerFlow = triggerFlow;
    internal void SetTriggerKeyInternal(string? triggerKey) => TriggerKey = triggerKey;
    internal void SetTriggerInstanceIdInternal(string? triggerInstanceId) => TriggerInstanceId = triggerInstanceId;
    internal void SetBodyInternal(JsonElement? body) => Body = body;

    protected override void Configure(JsonElement config)
    {
        base.Configure(config);

        if (config.TryGetProperty("transitionName", out var transitionNameElement))
            TransitionName = transitionNameElement.GetString();

        if (config.TryGetProperty("domain", out var triggerDomainElement))
            TriggerDomain = triggerDomainElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerDomain))
            throw new ArgumentException("Property 'domain' is required for DirectTriggerTask.", nameof(config));

        if (config.TryGetProperty("flow", out var triggerFlowElement))
            TriggerFlow = triggerFlowElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerFlow))
            throw new ArgumentException("Property 'flow' is required for DirectTriggerTask.", nameof(config));

        if (config.TryGetProperty("key", out var keyElement))
            TriggerKey = keyElement.GetString();

        if (config.TryGetProperty("instanceId", out var instanceIdElement))
            TriggerInstanceId = instanceIdElement.GetString();

        if (config.TryGetProperty("body", out var bodyElement))
        {
            var body = bodyElement.GetRawText();
            Body = string.IsNullOrWhiteSpace(body) ? null : bodyElement;
        }
    }

    public static DirectTriggerTask Create(JsonElement config)
    {
        return new DirectTriggerTask(config);
    }

    /// <summary>
    /// Creates a deep copy of the current DirectTriggerTask instance.
    /// </summary>
    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }

    /// <summary>
    /// Creates a typed deep copy of the current DirectTriggerTask instance.
    /// </summary>
    public DirectTriggerTask CloneTyped()
    {
        var cloned = new DirectTriggerTask();
        CopyBaseTo(cloned);

        cloned.TransitionName = TransitionName;
        cloned.TriggerDomain = TriggerDomain;
        cloned.TriggerFlow = TriggerFlow;
        cloned.TriggerKey = TriggerKey;
        cloned.TriggerInstanceId = TriggerInstanceId;
        cloned.Body = Body;

        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(DirectTriggerTask source)
    {
        source.CopyBaseToInternal(this);
        SetTransitionNameInternal(source.TransitionName);
        SetTriggerDomainInternal(source.TriggerDomain);
        SetTriggerFlowInternal(source.TriggerFlow);
        SetTriggerKeyInternal(source.TriggerKey);
        SetTriggerInstanceIdInternal(source.TriggerInstanceId);
        SetBodyInternal(source.Body);
    }

    /// <summary>
    /// Resets the task instance to a clean state for object pooling
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        TransitionName = null;
        TriggerDomain = string.Empty;
        TriggerFlow = string.Empty;
        TriggerKey = null;
        TriggerInstanceId = null;
        Body = null;
    }

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static DirectTriggerTask CreateEmpty()
    {
        return new DirectTriggerTask();
    }
}

