using System.Text.Json;

namespace BBT.Workflow.ClickHouse;

/// <summary>
/// ClickHouse instance data model
/// </summary>
public class ClickHouseInstanceData
{
    /// <summary>
    /// Instance ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Instance key
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Flow name
    /// </summary>
    public string Flow { get; set; } = string.Empty;

    /// <summary>
    /// Current state
    /// </summary>
    public string? CurrentState { get; set; }

    /// <summary>
    /// Status code
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Created at timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Modified at timestamp
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Completed at timestamp
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Tags as JSON string
    /// </summary>
    public string Tags { get; set; } = "[]";

    /// <summary>
    /// Is transient flag
    /// </summary>
    public bool IsTransient { get; set; }

    /// <summary>
    /// Operation type (Insert/Update)
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Transfer timestamp
    /// </summary>
    public DateTime TransferTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ClickHouse instance transition data model
/// </summary>
public class ClickHouseInstanceTransitionData
{
    /// <summary>
    /// Transition ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Instance ID
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Transition ID string
    /// </summary>
    public string TransitionId { get; set; } = string.Empty;

    /// <summary>
    /// From state
    /// </summary>
    public string FromState { get; set; } = string.Empty;

    /// <summary>
    /// To state
    /// </summary>
    public string? ToState { get; set; }

    /// <summary>
    /// Started at timestamp
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Finished at timestamp
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Body as JSON string
    /// </summary>
    public string Body { get; set; } = "{}";

    /// <summary>
    /// Header as JSON string
    /// </summary>
    public string Header { get; set; } = "{}";

    /// <summary>
    /// Operation type (Insert/Update)
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Transfer timestamp
    /// </summary>
    public DateTime TransferTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ClickHouse instance task data model
/// </summary>
public class ClickHouseInstanceTaskData
{
    /// <summary>
    /// Task ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Transition ID
    /// </summary>
    public Guid TransitionId { get; set; }

    /// <summary>
    /// Task ID string
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Started at timestamp
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Finished at timestamp
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Faulted task ID
    /// </summary>
    public Guid? FaultedTaskId { get; set; }

    /// <summary>
    /// Request as JSON string
    /// </summary>
    public string Request { get; set; } = "{}";

    /// <summary>
    /// Response as JSON string
    /// </summary>
    public string Response { get; set; } = "{}";

    /// <summary>
    /// Operation type (Insert/Update)
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Transfer timestamp
    /// </summary>
    public DateTime TransferTimestamp { get; set; } = DateTime.UtcNow;
}

