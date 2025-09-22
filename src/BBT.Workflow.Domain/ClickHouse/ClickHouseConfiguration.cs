namespace BBT.Workflow.ClickHouse;

/// <summary>
/// ClickHouse configuration settings
/// </summary>
public class ClickHouseConfiguration
{
    /// <summary>
    /// Whether ClickHouse integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// ClickHouse connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Batch size for bulk operations
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Flush interval in seconds
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Table name mappings
    /// </summary>
    public ClickHouseTableNames Tables { get; set; } = new();
}

/// <summary>
/// ClickHouse table name mappings
/// </summary>
public class ClickHouseTableNames
{
    /// <summary>
    /// Instances table name
    /// </summary>
    public string Instances { get; set; } = "instances";

    /// <summary>
    /// Instance transitions table name
    /// </summary>
    public string InstanceTransitions { get; set; } = "instance_transitions";

    /// <summary>
    /// Instance tasks table name
    /// </summary>
    public string InstanceTasks { get; set; } = "instance_tasks";
}

