using System.Text.RegularExpressions;
using BBT.Aether;
using BBT.Aether.Auditing;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances.Policies;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Instances;

/// <summary>
/// Instance
/// </summary>
public sealed class Instance : AggregateRoot<Guid>, IHasCreatedAt, IHasModifyTime, IObjectDictionary
{
    private Instance()
    {
    }

    internal Instance(
        Guid id,
        string flow,
        string? key
    ) : base(id)
    {
        IsTransient = true;
        CreatedAt = DateTime.UtcNow;
        Flow = Check.NotNull(flow, nameof(Flow), WorkflowConstants.MaxKeyLength);
        Key = Check.Length(key, nameof(Key), InstanceConstants.MaxKeyLength);
        Status = InstanceStatus.Active;

        Tags = [];

        MetaData = new ObjectDictionary();

        _dataList = [];
    }

    public static Instance Create(
        Guid id,
        string flow,
        string? key = null
    )
    {
        return new Instance(
            id,
            flow,
            key
        );
    }

    /// <summary>
    /// It is the key value for the heat flow.
    /// </summary>
    public string? Key { get; private set; }

    /// <summary>
    /// Flow key.
    /// </summary>
    public string Flow { get; private set; }

    /// <summary>
    /// Current state key
    /// </summary>
    public string? CurrentState { get; private set; }

    public string GetCurrentState => string.IsNullOrWhiteSpace(CurrentState) ? string.Empty : CurrentState;

    /// <summary>
    /// Status
    /// </summary>
    public InstanceStatus Status { get; private set; }

    /// <summary>
    /// Completed at
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    public bool IsCompleted =>
        Status.Equals(InstanceStatus.Completed)
        || Status.Equals(InstanceStatus.Faulted)
        || Status.Equals(InstanceStatus.Passive);

    public bool IsBusy => Status.Equals(InstanceStatus.Busy);
    public bool IsActive => Status.Equals(InstanceStatus.Active);
    public bool IsSubFlow => this.ToFlowType()?.Equals(WorkflowType.SubFlow) ?? false;
    public bool IsSubItem => (this.ToFlowType()?.Equals(WorkflowType.SubFlow) ?? false) || (this.ToFlowType()?.Equals(WorkflowType.SubProcess) ?? false);
    public bool IsActiveSubFlow => _childCorrelations.Any(p => p.IsCompleted && p.SubFlowType.Equals(SubFlowType.SubFlow));
    public TimeSpan? Duration { get; private set; }
    public List<string> Tags { get; private set; }

    /// <summary>
    /// Created at
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Modified at
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    public bool IsTransient { get; private set; }

    public ObjectDictionary MetaData { get; set; }

    public void SetMetaData(ObjectDictionary data)
    {
        MetaData = data;
    }

    private readonly List<InstanceData> _dataList = new();
    private readonly object _dataListLock = new(); // Thread-safe lock for data operations

    /// <summary>
    /// Child Correlations
    /// </summary>
    public IReadOnlyCollection<InstanceData> DataList => _dataList.AsReadOnly();

    /// <summary>
    /// Latest data
    /// </summary>
    public dynamic? Data
    {
        get
        {
            lock (_dataListLock)
            {
                return _dataList.OrderByDescending(x => x, InstanceDataVersionComparer.Instance).FirstOrDefault()
                    ?.Attributes;
            }
        }
    }

    public InstanceData? LatestData
    {
        get
        {
            lock (_dataListLock)
            {
                return _dataList.OrderByDescending(x => x, InstanceDataVersionComparer.Instance).FirstOrDefault();
            }
        }
    }

    private readonly List<InstanceCorrelation> _childCorrelations = new();

    /// <summary>
    /// Child Correlations
    /// </summary>
    public IReadOnlyCollection<InstanceCorrelation> ChildCorrelations => _childCorrelations.AsReadOnly();

    public void Complete()
    {
        Status = InstanceStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Duration = CompletedAt - CreatedAt;
    }

    public void Fault()
    {
        Status = InstanceStatus.Faulted;
        CompletedAt = DateTime.UtcNow;
        Duration = CompletedAt - CreatedAt;
    }

    /// <summary>
    /// Sets the instance status to Busy.
    /// This is typically called when a transition is being processed to prevent concurrent modifications.
    /// </summary>
    public void Busy()
    {
        Status = InstanceStatus.Busy;
    }

    /// <summary>
    /// Sets the instance status to Active.
    /// This is typically called when a transition processing is completed successfully.
    /// </summary>
    public void Active()
    {
        Status = InstanceStatus.Active;
    }

    public void SetToActiveOrBusyBasedOnSubFlow()
    {
        if (IsCompleted) 
            return;
            
        if (IsActiveSubFlow)
        {
            Busy();
            return;
        }
        
        Active();
    }

    public void AddCorrelation(InstanceCorrelation correlation)
    {
        _childCorrelations.Add(correlation);
    }

    public void SetKey(string key)
    {
        Key = Check.NotNullOrEmpty(key, nameof(key), InstanceConstants.MaxKeyLength);
    }

    private void SetState(string currentState)
    {
        CurrentState = Check.Length(currentState, nameof(currentState), StateConstants.MaxKeyLength);
    }

    public void ChangeState(State state)
    {
        SetState(state.Key);
    }

    public void ChangeState(Transition transition)
    {
        SetState(transition.Target);
    }

    public void ChangeState(WorkflowTimeout timeout)
    {
        SetState(timeout.Target);
    }

    public void AddTags(string[]? tags)
    {
        tags ??= [];

        Tags.RemoveAll(existingTag => !tags.Contains(existingTag));

        foreach (var tag in tags)
        {
            if (!Tags.Contains(tag))
            {
                Tags.Add(tag);
            }
        }
    }

    public bool CanExecuteTransition(Transition transition, State state, StateTransitionPolicy policy,
        WorkflowExecutionContext executionContext = WorkflowExecutionContext.User)
    {
        policy.Validate(state, transition, executionContext);
        return true;
    }

    public InstanceData AddDataWithVersion(Guid id, JsonData inputData, string version)
    {
        var newData = new InstanceData(
            id,
            Id,
            version,
            inputData, true
        );
        _dataList.Add(newData);
        return newData;
    }

    public InstanceData AddData(Guid id, JsonData inputData, VersionStrategy? versionStrategy = null)
    {
        lock (_dataListLock)
        {
            var lastData = _dataList.LastOrDefault();
            InstanceData newData = lastData is null
                ? new InstanceData(
                    id,
                    Id,
                    WorkflowConstants.DefaultVersion,
                    inputData,
                    true
                )
                : lastData.NewVersion(
                    id,
                    inputData,
                    versionStrategy ?? VersionStrategy.IncreaseMinor
                );
            _dataList.Add(newData);
            return newData;
        }
    }

    public InstanceData? FindData(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;

        lock (_dataListLock)
        {
            var exactMatch = _dataList.FirstOrDefault(x => x.Version == version);
            if (exactMatch != null)
                return exactMatch;

            // Partial versiyon: 1.0 → tüm 1.0.x versiyonları içinde en büyük olanı bul
            var match = Regex.Match(version, @"^(\d+)\.(\d+)$");
            if (match.Success)
            {
                var prefix = $"{match.Groups[1].Value}.{match.Groups[2].Value}.";

                var matched = _dataList
                    .Where(d => d.Version.StartsWith(prefix))
                    .OrderByDescending(d => d, InstanceDataVersionComparer.Instance)
                    .FirstOrDefault();

                return matched;
            }

            return null;
        }
    }
}