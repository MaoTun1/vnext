namespace BBT.Workflow.Execution.Configuration;

/// <summary>
/// Configuration options for Trigger Tasks retry policy.
/// </summary>
public sealed class TriggerRetryOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "TriggerRetry";

    /// <summary>
    /// Maximum retry attempts (default: 3)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds (default: 2000ms)
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 2000;

    /// <summary>
    /// HTTP status codes that should trigger a retry (default: [409])
    /// </summary>
    public int[] RetryOnStatusCodes { get; set; } = [409];

    /// <summary>
    /// Enable jitter for retry delays to prevent thundering herd (default: true)
    /// </summary>
    public bool UseJitter { get; set; } = true;
}