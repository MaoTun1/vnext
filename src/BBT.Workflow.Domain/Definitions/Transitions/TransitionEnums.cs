namespace BBT.Workflow.Definitions;

/// <summary>
/// Trigger types that can initiate a workflow transition.
/// </summary>
public enum TriggerType
{
    /// <summary>
    /// Manual trigger - initiated by user or external API call.
    /// </summary>
    Manual = 0,
    
    /// <summary>
    /// Automatic trigger - initiated by system based on conditions or rules.
    /// </summary>
    Automatic = 1,
    
    /// <summary>
    /// Scheduled trigger - initiated by timer or cron expression.
    /// </summary>
    Scheduled = 2,
    
    /// <summary>
    /// Event trigger - initiated by external event or message.
    /// </summary>
    Event = 3
} 