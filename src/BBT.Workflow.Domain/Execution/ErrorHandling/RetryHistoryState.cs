using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Execution.ErrorHandling;

/// <summary>
/// Represents the complete retry history state for an instance.
/// Stored in Instance.ExtraProperties to persist across process restarts.
/// Tracks both current retry attempts and historical entries.
/// </summary>
public sealed record RetryHistoryState
{
    /// <summary>
    /// Key used to store retry history in Instance.ExtraProperties.
    /// </summary>
    public const string ExtraPropertiesKey = "RetryHistory";

    /// <summary>
    /// Maximum number of history entries to keep per scope.
    /// Prevents unbounded growth of ExtraProperties.
    /// </summary>
    public const int MaxHistoryEntriesPerScope = 50;

    /// <summary>
    /// List of all retry history entries.
    /// </summary>
    [JsonPropertyName("entries")]
    public List<RetryHistoryEntry> Entries { get; init; } = [];

    /// <summary>
    /// Current retry attempt counts by scope key.
    /// Key format: "transition:step" or "transition:step:task"
    /// </summary>
    [JsonPropertyName("currentAttempts")]
    public Dictionary<string, int> CurrentAttempts { get; init; } = [];

    /// <summary>
    /// Maximum retries allowed per scope.
    /// </summary>
    [JsonPropertyName("maxRetries")]
    public Dictionary<string, int> MaxRetries { get; init; } = [];

    /// <summary>
    /// Generates a scope key for a step-level retry.
    /// </summary>
    public static string GetScopeKey(string transitionKey, string stepName)
        => $"{transitionKey}:{stepName}";

    /// <summary>
    /// Generates a scope key for a task-level retry.
    /// </summary>
    public static string GetScopeKey(string transitionKey, string stepName, string taskKey)
        => $"{transitionKey}:{stepName}:{taskKey}";

    /// <summary>
    /// Gets the current attempt count for a scope.
    /// </summary>
    public int GetCurrentAttempt(string scopeKey)
        => CurrentAttempts.TryGetValue(scopeKey, out var attempt) ? attempt : 0;

    /// <summary>
    /// Checks if max retries have been exceeded for a scope.
    /// </summary>
    public bool IsMaxRetriesExceeded(string scopeKey)
    {
        var current = GetCurrentAttempt(scopeKey);
        var max = MaxRetries.TryGetValue(scopeKey, out var maxVal) ? maxVal : 0;
        return current >= max;
    }

    /// <summary>
    /// Gets retry entries for a specific scope.
    /// </summary>
    public IEnumerable<RetryHistoryEntry> GetEntriesForScope(string transitionKey)
        => Entries.Where(e => e.TransitionKey == transitionKey);

    /// <summary>
    /// Adds a new retry entry and updates the current attempt count.
    /// </summary>
    public RetryHistoryState AddEntry(RetryHistoryEntry entry, int maxRetries)
    {
        var scopeKey = string.IsNullOrEmpty(entry.TaskKey)
            ? GetScopeKey(entry.TransitionKey, entry.StepName)
            : GetScopeKey(entry.TransitionKey, entry.StepName, entry.TaskKey);

        var newEntries = new List<RetryHistoryEntry>(Entries) { entry };

        // Trim entries per transition if exceeding limit
        var transitionEntries = newEntries
            .Where(e => e.TransitionKey == entry.TransitionKey)
            .OrderByDescending(e => e.AttemptedAt)
            .Take(MaxHistoryEntriesPerScope)
            .ToList();

        var otherEntries = newEntries
            .Where(e => e.TransitionKey != entry.TransitionKey)
            .ToList();

        var newCurrentAttempts = new Dictionary<string, int>(CurrentAttempts);
        var currentAttempt = GetCurrentAttempt(scopeKey);
        newCurrentAttempts[scopeKey] = currentAttempt + 1;

        var newMaxRetries = new Dictionary<string, int>(MaxRetries)
        {
            [scopeKey] = maxRetries
        };

        return this with
        {
            Entries = [.. otherEntries, .. transitionEntries],
            CurrentAttempts = newCurrentAttempts,
            MaxRetries = newMaxRetries
        };
    }

    /// <summary>
    /// Marks a scope as successful, clearing its retry state.
    /// </summary>
    public RetryHistoryState MarkScopeSuccessful(string scopeKey)
    {
        var newCurrentAttempts = new Dictionary<string, int>(CurrentAttempts);
        newCurrentAttempts.Remove(scopeKey);

        var newMaxRetries = new Dictionary<string, int>(MaxRetries);
        newMaxRetries.Remove(scopeKey);

        // Mark the last entry for this scope as successful
        var entries = Entries.ToList();
        var lastEntry = entries
            .Where(e => 
            {
                var key = string.IsNullOrEmpty(e.TaskKey)
                    ? GetScopeKey(e.TransitionKey, e.StepName)
                    : GetScopeKey(e.TransitionKey, e.StepName, e.TaskKey);
                return key == scopeKey;
            })
            .OrderByDescending(e => e.AttemptedAt)
            .FirstOrDefault();

        if (lastEntry != null)
        {
            var index = entries.IndexOf(lastEntry);
            if (index >= 0)
            {
                entries[index] = lastEntry.MarkSuccessful();
            }
        }

        return this with
        {
            Entries = entries,
            CurrentAttempts = newCurrentAttempts,
            MaxRetries = newMaxRetries
        };
    }

    /// <summary>
    /// Clears all retry state for a specific transition.
    /// Called when a transition completes successfully.
    /// </summary>
    public RetryHistoryState ClearTransition(string transitionKey)
    {
        var newCurrentAttempts = CurrentAttempts
            .Where(kvp => !kvp.Key.StartsWith($"{transitionKey}:"))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var newMaxRetries = MaxRetries
            .Where(kvp => !kvp.Key.StartsWith($"{transitionKey}:"))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return this with
        {
            CurrentAttempts = newCurrentAttempts,
            MaxRetries = newMaxRetries
        };
    }

    /// <summary>
    /// Creates an empty retry history state.
    /// </summary>
    public static RetryHistoryState Empty => new();

    /// <summary>
    /// Serializes this state to JSON for storage in ExtraProperties.
    /// </summary>
    public string ToJson()
        => JsonSerializer.Serialize(this, JsonSerializerOptions);

    /// <summary>
    /// Deserializes from JSON stored in ExtraProperties.
    /// </summary>
    public static RetryHistoryState? FromJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<RetryHistoryState>(json, JsonSerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deserializes from an object (handles JsonElement from ExtraProperties).
    /// </summary>
    public static RetryHistoryState? FromObject(object? obj)
    {
        if (obj == null)
            return null;

        if (obj is RetryHistoryState state)
            return state;

        if (obj is JsonElement element)
            return JsonSerializer.Deserialize<RetryHistoryState>(element.GetRawText(), JsonSerializerOptions);

        if (obj is string json)
            return FromJson(json);

        return null;
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

