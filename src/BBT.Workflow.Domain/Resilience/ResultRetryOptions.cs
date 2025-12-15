namespace BBT.Workflow.Resilience;

/// <summary>
/// Configuration options for Result-based retry policies.
/// Used for both local and remote operations that return Result&lt;T&gt;.
/// </summary>
public sealed class ResultRetryOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "ResultRetry";

    /// <summary>
    /// Maximum retry attempts (default: 3)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds (default: 50ms)
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 50;

    /// <summary>
    /// Error codes that should trigger a retry (default: TransitionLocked, Locked)
    /// </summary>
    public string[] RetryOnErrorCodes { get; set; } =
    [
        WorkflowErrorCodes.TransitionLocked,
        WorkflowErrorCodes.Locked
    ];

    /// <summary>
    /// Enable jitter for retry delays to prevent thundering herd (default: true)
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Backoff type for retry delays (default: Constant)
    /// Options: Constant, Linear, Exponential
    /// </summary>
    public string BackoffType { get; set; } = "Constant";
}

