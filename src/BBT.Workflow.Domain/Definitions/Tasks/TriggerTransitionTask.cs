using System.Text.Json;
using System.Text.Json.Serialization;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Trigger Transition Task Definition
/// </summary>
public sealed class TriggerTransitionTask : WorkflowTask
{
    private TriggerTransitionTask()
    {
    }

    [JsonConstructor]
    private TriggerTransitionTask(
        JsonElement config) : base(config)
    {
        Type = ((int)TaskType.TriggerTransition).ToString();
    }

    /// <summary>
    /// Transition name to execute (required for Direct and Correlation trigger types)
    /// </summary>
    public string? TransitionName { get; private set; }

    /// <summary>
    /// Body data to send with the transition request
    /// </summary>
    public JsonElement? Body { get; private set; }

    /// <summary>
    /// Domain of the target workflow
    /// </summary>
    public string TriggerDomain { get; private set; } = string.Empty;

    /// <summary>
    /// Flow name of the target workflow
    /// </summary>
    public string TriggerFlow { get; private set; } = string.Empty;
    /// <summary>
    /// Flow key of the target workflow
    /// </summary>
    public string? TriggerKey { get; private set; } = string.Empty;
    /// <summary>
    /// InstanceId of the target workflow
    /// </summary>
    public string? TriggerInstanceId { get; private set; } = string.Empty;

    /// <summary>
    /// Trigger type
    /// </summary>
    public TriggerTransitionType TriggerType { get; private set; }


    /// <summary>
    /// SubFlow version for SubProcess trigger type (optional)
    /// </summary>
    public string? TriggerVersion { get; private set; }

    /// <summary>
    /// Extensions to request for GetInstanceData trigger type (optional)
    /// </summary>
    public string[]? Extensions { get; private set; }

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
    public void SetTriggerType(string type)
    {
        if (!string.IsNullOrWhiteSpace(type))
        {
            TriggerType = Enum.Parse<TriggerTransitionType>(type, ignoreCase: true);
        }
    }

    /// <summary>
    /// Internal property setters for object pooling
    /// </summary>
    internal void SetTransitionNameInternal(string? transitionName) => TransitionName = transitionName;
    internal void SetBodyInternal(JsonElement? body) => Body = body;
    internal void SetTriggerDomainInternal(string triggerDomain) => TriggerDomain = triggerDomain;
    internal void SetTriggerFlowInternal(string triggerFlow) => TriggerFlow = triggerFlow;
    internal void SetTriggerKeyInternal(string? triggerKey) => TriggerKey = triggerKey;
    internal void SetTriggerInstanceIdInternal(string? triggerInstanceId) => TriggerInstanceId = triggerInstanceId;
    internal void SetTriggerTypeInternal(TriggerTransitionType triggerType) => TriggerType = triggerType;
    internal void SetTriggerVersionInternal(string? triggerVersion) => TriggerVersion = triggerVersion;
    internal void SetExtensionsInternal(string[]? extensions) => Extensions = extensions;

    protected override void Configure(JsonElement config)
    {
        base.Configure(config);

        if (config.TryGetProperty("transitionName", out var transitionNameElement))
            TransitionName = transitionNameElement.GetString();

        if (config.TryGetProperty("body", out var bodyElement))
        {
            var body = bodyElement.GetRawText();
            Body = string.IsNullOrWhiteSpace(body) ? null : bodyElement;
        }

        if (config.TryGetProperty("domain", out var triggerDomainElement))
            TriggerDomain = triggerDomainElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerDomain))
            throw new ArgumentException("Property 'domain' is required for TriggerTransitionTask.", nameof(config));

        if (config.TryGetProperty("flow", out var triggerFlowElement))
            TriggerFlow = triggerFlowElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(TriggerFlow))
            throw new ArgumentException("Property 'flow' is required for TriggerTransitionTask.", nameof(config));

        if (config.TryGetProperty("type", out var triggerTypeElement))
        {
            var triggerTypeStr = triggerTypeElement.GetString();
            if (!string.IsNullOrWhiteSpace(triggerTypeStr))
            {
                TriggerType = Enum.Parse<TriggerTransitionType>(triggerTypeStr, ignoreCase: true);
            }
        }



        if (config.TryGetProperty("version", out var TriggerVersionElement))
            TriggerVersion = TriggerVersionElement.GetString();

        if (config.TryGetProperty("key", out var keyElement))
            TriggerKey = keyElement.GetString();

        if (config.TryGetProperty("instanceId", out var instanceIdElement))
            TriggerInstanceId = instanceIdElement.GetString();

        if (config.TryGetProperty("extensions", out var extensionsElement))
        {
            if (extensionsElement.ValueKind == JsonValueKind.Array)
            {
                var extensionsList = new List<string>();
                foreach (var item in extensionsElement.EnumerateArray())
                {
                    var ext = item.GetString();
                    if (!string.IsNullOrWhiteSpace(ext))
                        extensionsList.Add(ext);
                }
                Extensions = extensionsList.Count > 0 ? extensionsList.ToArray() : null;
            }
        }
    }

    public static TriggerTransitionTask Create(
        JsonElement config)
    {
        return new TriggerTransitionTask(config);
    }

    /// <summary>
    /// Creates a deep copy of the current TriggerTransitionTask instance.
    /// </summary>
    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }

    /// <summary>
    /// Creates a typed deep copy of the current TriggerTransitionTask instance.
    /// </summary>
    public TriggerTransitionTask CloneTyped()
    {
        var cloned = new TriggerTransitionTask();
        CopyBaseTo(cloned);

        cloned.TransitionName = TransitionName;
        cloned.Body = Body;
        cloned.TriggerDomain = TriggerDomain;
        cloned.TriggerFlow = TriggerFlow;
        cloned.TriggerKey = TriggerKey;
        cloned.TriggerInstanceId = TriggerInstanceId;
        cloned.TriggerType = TriggerType;
        cloned.TriggerVersion = TriggerVersion;
        cloned.Extensions = Extensions;

        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(TriggerTransitionTask source)
    {
        source.CopyBaseToInternal(this);
        SetTransitionNameInternal(source.TransitionName);
        SetBodyInternal(source.Body);
        SetTriggerDomainInternal(source.TriggerDomain);
        SetTriggerFlowInternal(source.TriggerFlow);
        SetTriggerKeyInternal(source.TriggerKey);
        SetTriggerInstanceIdInternal(source.TriggerInstanceId);
        SetTriggerTypeInternal(source.TriggerType);
        SetTriggerVersionInternal(source.TriggerVersion);
        SetExtensionsInternal(source.Extensions);
    }

    /// <summary>
    /// Resets the task instance to a clean state for object pooling
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        TransitionName = null;
        Body = null;
        TriggerDomain = string.Empty;
        TriggerFlow = string.Empty;
        TriggerKey = null;
        TriggerInstanceId = null;
        TriggerType = TriggerTransitionType.Trigger;
        TriggerVersion = null;
        Extensions = null;
    }

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static TriggerTransitionTask CreateEmpty()
    {
        return new TriggerTransitionTask();
    }
}

/// <summary>
/// Trigger transition type enumeration
/// </summary>
public enum TriggerTransitionType
{

    /// <summary>
    /// Create a new workflow instance
    /// </summary>
    Start = 1,

    /// <summary>
    /// Direct transition on the current instance
    /// </summary>
    Trigger = 2,

    /// <summary>
    /// Subprocess trigger
    /// </summary>
    SubProcess = 3,

    /// <summary>
    /// GetInstanceData trigger
    /// </summary>
    GetInstanceData = 4
}

