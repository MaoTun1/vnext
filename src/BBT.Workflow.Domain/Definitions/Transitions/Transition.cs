using System.Text.Json.Serialization;
using BBT.Aether;
using BBT.Workflow.Instances.Policies;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Transition definition
/// </summary>
public sealed class Transition : IHasKey
{
    private Transition()
    {
    }

    private Transition(
        string key,
        string? from,
        string target,
        TriggerType triggerType
    )
    {
        SetKey(key);
        SetFrom(from);
        SetTarget(target);
        TriggerType = triggerType;

        AvailableIn = [];
        labels = [];
        onExecutionTasks = [];
    }

    [JsonConstructor]
    private Transition(
        string key,
        string? from,
        string target,
        TriggerType triggerType,
        VersionStrategy versionStrategy,
        List<LanguageLabel> labels,
        List<OnExecuteTask> onExecutionTasks
    ) : this(key, from, target, triggerType)
    {
        VersionStrategy = versionStrategy;
        this.labels = labels ?? [];
        this.onExecutionTasks = onExecutionTasks ?? [];
    }

    /// <summary>
    /// If present, it is the more readable key value of the record.
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// From state information.
    /// </summary>
    public string? From { get; private set; } = string.Empty;

    /// <summary>
    /// Specifies the targeted state information.
    /// </summary>
    public string Target { get; private set; } = string.Empty;

    /// <summary>
    /// Version Strategy
    /// </summary>
    public VersionStrategy VersionStrategy { get; private set; }

    /// <summary>
    /// <see cref="TriggerType"/>
    /// </summary>
    public TriggerType TriggerType { get; private set; }
    [JsonInclude] public ScriptCode? Timer { get; private set; }
    [JsonInclude] public ScriptCode? Rule { get; private set; }
    [JsonInclude] public Reference? Schema { get; private set; }
    [JsonInclude] public List<string> AvailableIn { get; private set; }

    [JsonInclude] [JsonPropertyName("labels")]
    private List<LanguageLabel> labels = new();

    [JsonInclude] [JsonPropertyName("onExecutionTasks")]
    private List<OnExecuteTask> onExecutionTasks = new();

    /// <summary>
    /// Language
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<LanguageLabel> Labels => labels.AsReadOnly();

    /// <summary>
    /// Transition View
    /// </summary>
    public IReference? View { get; private set; }

    /// <summary>
    /// On Execution Tasks
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<OnExecuteTask> OnExecutionTasks => onExecutionTasks.AsReadOnly();

    private void SetKey(string key)
    {
        Key = Check.NotNullOrEmpty(key, nameof(Key), TransitionConstants.MaxKeyLength);
    }

    private void SetTarget(string target)
    {
        Target = Check.NotNull(target, nameof(Target), TransitionConstants.MaxTargetLength);
    }

    private void SetFrom(string? from)
    {
        From = Check.Length(from, nameof(From), TransitionConstants.MaxTargetLength);
    }

    public void AddLanguage(string label, string language)
    {
        if (labels.All(l => l.Language != language))
        {
            labels.Add(new LanguageLabel(label, language));
        }
        else
        {
            var languageLabel = labels.First(p => p.Language == language);
            labels.Remove(languageLabel);
            labels.Add(new LanguageLabel(label, language));
        }
    }

    public void AddAvailableIn(string stateKey)
    {
        if (!AvailableIn.Contains(stateKey))
        {
            AvailableIn.Add(stateKey);
        }
    }

    public void SetView(IReference reference)
    {
        View = reference;
    }

    public void AddOnExecutionTask(OnExecuteTask task)
    {
        onExecutionTasks.Add(task);
    }

    public void SetSchema(IReference reference)
    {
        Schema = reference.ToReference();
    }

    public void SetRule(string location, string scriptCode)
    {
        Rule = new ScriptCode(location, scriptCode);
    }
    
    public void SetTimer(string location, string code)
    {
        Timer = new ScriptCode(location, code);
    }

    public bool CanExecute(State currentState, StateTransitionPolicy policy)
    {
        policy.Validate(currentState, this);
        return true;
    }

    public static Transition Create(
        string key,
        string? from,
        string target,
        TriggerType triggerType,
        string versionStrategy)
    {
        return new Transition(
            key,
            from,
            target,
            triggerType)
        {
            VersionStrategy = VersionStrategy.FromCode(versionStrategy)
        };
    }
}