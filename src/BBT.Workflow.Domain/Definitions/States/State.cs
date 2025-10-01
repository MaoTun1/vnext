using System.Text.Json.Serialization;
using BBT.Aether;

namespace BBT.Workflow.Definitions;

/// <summary>
/// It is in the possible statuses found in the flow.
/// </summary>
public sealed class State : IHasKey
{
    private State()
    {
    }

    private State(
        string key,
        StateType stateType)
    {
        SetKey(key);
        StateType = stateType;
    
        labels = [];
        onEntries = [];
        onExits = [];
    }

    [JsonConstructor]
    private State(
        string key,
        StateType stateType,
        VersionStrategy versionStrategy,
        List<LanguageLabel>? labels,
        List<OnExecuteTask>? onEntries,
        List<OnExecuteTask>? onExits,
         Reference view)
        : this(key, stateType)
    {
        VersionStrategy = versionStrategy;
        this.labels = labels ?? [];
        this.onEntries = onEntries ?? [];
        this.onExits = onExits ?? [];
        this.view = view;
    }

    /// <summary>
    /// If present, it is the more readable key value of the record.
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    /// Version strategy
    /// </summary>
    public VersionStrategy VersionStrategy { get; private set; }

    /// <summary>
    /// <see cref="StateType"/>
    /// </summary>
    public StateType StateType { get; private set; }

    [JsonInclude] [JsonPropertyName("labels")]
    private List<LanguageLabel> labels = new();

    [JsonInclude] [JsonPropertyName("transitions")]
    private List<Transition> transitions = new();

    [JsonInclude] [JsonPropertyName("onEntries")]
    private List<OnExecuteTask> onEntries = new();

    [JsonInclude] [JsonPropertyName("onExits")]
    private List<OnExecuteTask> onExits = new();

    [JsonInclude] [JsonPropertyName("subFlow")]
    public SubFlow? SubFlow { get; private set; }
    [JsonInclude] [JsonPropertyName("view")]
    public Reference? view  { get; private set; }

    /// <summary>
    /// Languages
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<LanguageLabel> Labels => labels.AsReadOnly();

    /// <summary>
    /// State view
    /// </summary>
    [JsonIgnore]
    public Reference? View => view?.ToReference();

    /// <summary>
    /// Transitions
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<Transition> Transitions => transitions.AsReadOnly();

    /// <summary>
    /// On entries
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<OnExecuteTask> OnEntries => onEntries.AsReadOnly();

    /// <summary>
    /// On exits
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<OnExecuteTask> OnExits => onExits.AsReadOnly();

    private void SetKey(string key)
    {
        Key = Check.NotNullOrEmpty(key, nameof(Key), StateConstants.MaxKeyLength);
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

    public void AddOnEntry(OnExecuteTask task)
    {
        onEntries.Add(task);
    }

    public void AddOnExit(OnExecuteTask task)
    {
        onExits.Add(task);
    }

    public void SetView(IReference view)
    {
        view = view.ToReference();
    }
    
    public void SetSubFlow(string type, IReference reference, ScriptCode mapping)
    {
        SubFlow = SubFlow.Create(type, reference, mapping);
    }

    public void AddTransition(Transition transition)
    {
        transitions.Add(transition);
    }

    public Transition? FindTransition(string key)
    {
        return Transitions.FirstOrDefault(t => t.Key == key);
    }

    public IEnumerable<Transition> GetAutoTransitions()
    {
        return Transitions.Where(p => p.TriggerType == TriggerType.Automatic);
    }
    
    public IEnumerable<Transition> GetScheduledTransitions()
    {
        return Transitions.Where(p => p.TriggerType == TriggerType.Scheduled);
    }

    public List<string> TransitionKeys()
    {
        return Transitions.Select(t => t.Key).ToList();
    }

    public static State Create(
        string key,
        StateType stateType,
        string versionStrategy
    )
    {
        return new State(
            key,
            stateType)
        {
            VersionStrategy = VersionStrategy.FromCode(versionStrategy)
        };
    }
}