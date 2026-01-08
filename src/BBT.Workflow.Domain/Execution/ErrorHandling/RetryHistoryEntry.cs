using System.Text.Json.Serialization;

namespace BBT.Workflow.Execution.ErrorHandling;

/// <summary>
/// Represents a single retry attempt entry in the retry history.
/// Stored as part of RetryHistoryState in Instance.ExtraProperties.
/// </summary>
public sealed record RetryHistoryEntry
{
    /// <summary>
    /// The transition key where the retry occurred.
    /// </summary>
    [JsonPropertyName("transitionKey")]
    public string TransitionKey { get; init; } = string.Empty;

    /// <summary>
    /// The name of the step that was retried.
    /// </summary>
    [JsonPropertyName("stepName")]
    public string StepName { get; init; } = string.Empty;

    /// <summary>
    /// The specific task key that failed (if applicable).
    /// </summary>
    [JsonPropertyName("taskKey")]
    public string? TaskKey { get; init; }

    /// <summary>
    /// The retry attempt number (1-based).
    /// </summary>
    [JsonPropertyName("attempt")]
    public int Attempt { get; init; }

    /// <summary>
    /// The timestamp when this retry attempt was made.
    /// </summary>
    [JsonPropertyName("attemptedAt")]
    public DateTimeOffset AttemptedAt { get; init; }

    /// <summary>
    /// The error code from the failure.
    /// </summary>
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// The error message from the failure.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The HTTP status code if applicable.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; init; }

    /// <summary>
    /// The delay before this retry attempt.
    /// </summary>
    [JsonPropertyName("delayMs")]
    public long DelayMs { get; init; }

    /// <summary>
    /// Whether this retry attempt was successful.
    /// </summary>
    [JsonPropertyName("wasSuccessful")]
    public bool WasSuccessful { get; init; }

    /// <summary>
    /// The state key at the time of retry.
    /// </summary>
    [JsonPropertyName("stateKey")]
    public string? StateKey { get; init; }

    /// <summary>
    /// Gets the delay as TimeSpan.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Delay => TimeSpan.FromMilliseconds(DelayMs);

    /// <summary>
    /// Creates a new retry history entry.
    /// </summary>
    public static RetryHistoryEntry Create(
        string transitionKey,
        string stepName,
        string? taskKey,
        int attempt,
        string? errorCode,
        string? errorMessage,
        int? statusCode,
        TimeSpan delay,
        string? stateKey)
    {
        return new RetryHistoryEntry
        {
            TransitionKey = transitionKey,
            StepName = stepName,
            TaskKey = taskKey,
            Attempt = attempt,
            AttemptedAt = DateTimeOffset.UtcNow,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            DelayMs = (long)delay.TotalMilliseconds,
            WasSuccessful = false,
            StateKey = stateKey
        };
    }

    /// <summary>
    /// Creates a successful retry entry.
    /// </summary>
    public RetryHistoryEntry MarkSuccessful() => this with
    {
        WasSuccessful = true
    };
}

