using System.Text.Json;
using System.Text.Json.Serialization;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Trigger Transition Task Definition
/// </summary>
public sealed class TriggerTransitionTask : WorkflowTask, IGlobalTask
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
    /// Gets the mapping instance for this task. Returns null as TriggerTransitionTask uses script-based mapping.
    /// </summary>
    public IMapping? Mapping => null;

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
    public string TransitionDomain { get; private set; } = string.Empty;

    /// <summary>
    /// Flow name of the target workflow
    /// </summary>
    public string TransitionFlow { get; private set; } = string.Empty;

    /// <summary>
    /// Trigger type: Direct, Correlation, or CreateNew
    /// </summary>
    public TriggerTransitionType TriggerType { get; private set; }

    /// <summary>
    /// SubFlow name for Correlation trigger type (optional)
    /// </summary>
    public string? SubFlowName { get; private set; }
    public void SetBody(dynamic body)
    {
        Body = JsonSerializer.SerializeToElement(body);
    }

    /// <summary>
    /// Internal property setters for object pooling
    /// </summary>
    internal void SetTransitionNameInternal(string? transitionName) => TransitionName = transitionName;
    internal void SetBodyInternal(JsonElement? body) => Body = body;
    internal void SetTransitionDomainInternal(string transitionDomain) => TransitionDomain = transitionDomain;
    internal void SetTransitionFlowInternal(string transitionFlow) => TransitionFlow = transitionFlow;
    internal void SetTriggerTypeInternal(TriggerTransitionType triggerType) => TriggerType = triggerType;
    internal void SetSubFlowNameInternal(string? subFlowName) => SubFlowName = subFlowName;

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

        if (config.TryGetProperty("domain", out var transitionDomainElement))
            TransitionDomain = transitionDomainElement.GetString() ?? throw new ArgumentNullException(nameof(transitionDomainElement));

        if (config.TryGetProperty("flow", out var transitionFlowElement))
            TransitionFlow = transitionFlowElement.GetString() ?? throw new ArgumentNullException(nameof(transitionFlowElement));

        if (config.TryGetProperty("type", out var triggerTypeElement))
        {
            var triggerTypeStr = triggerTypeElement.GetString();
            if (!string.IsNullOrWhiteSpace(triggerTypeStr))
            {
                TriggerType = Enum.Parse<TriggerTransitionType>(triggerTypeStr, ignoreCase: true);
            }
        }

        if (config.TryGetProperty("subFlowName", out var subFlowNameElement))
            SubFlowName = subFlowNameElement.GetString();
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
        cloned.TransitionDomain = TransitionDomain;
        cloned.TransitionFlow = TransitionFlow;
        cloned.TriggerType = TriggerType;
        cloned.SubFlowName = SubFlowName;

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
        SetTransitionDomainInternal(source.TransitionDomain);
        SetTransitionFlowInternal(source.TransitionFlow);
        SetTriggerTypeInternal(source.TriggerType);
        SetSubFlowNameInternal(source.SubFlowName);
    }

    /// <summary>
    /// Resets the task instance to a clean state for object pooling
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        TransitionName = null;
        Body = null;
        TransitionDomain = string.Empty;
        TransitionFlow = string.Empty;
        TriggerType = TriggerTransitionType.Trigger;
        SubFlowName = null;
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

    SubProcess=3
}

