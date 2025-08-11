using BBT.Aether.Domain.Entities;

namespace BBT.Workflow.Instances;

public sealed class InstanceTransition : Entity<Guid>
{
    private InstanceTransition()
    {
    }

    public InstanceTransition(
        Guid id,
        Guid instanceId,
        string transitionId,
        string fromState,
        JsonData body,
        JsonData header
    ) : base(id)
    {
        InstanceId = instanceId;
        TransitionId = transitionId;
        FromState = fromState;
        StartedAt = DateTime.UtcNow;
        Body = body;
        Header = header;
    }

    public Guid InstanceId { get; private set; }
    public string TransitionId { get; private set; }
    public string FromState { get; private set; }
    public string? ToState { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public TimeSpan? Duration { get; private set; }

    /// <summary>
    /// Body
    /// <see cref="JsonData"/>
    /// </summary>
    public JsonData Body { get; private set; }

    /// <summary>
    /// Header
    /// <see cref="JsonData"/>
    /// </summary>
    public JsonData Header { get; private set; }
    
    public void Completed(string toState)
    {
        ToState = toState;
        FinishedAt = DateTime.UtcNow;
        Duration = FinishedAt - StartedAt;
    }
}