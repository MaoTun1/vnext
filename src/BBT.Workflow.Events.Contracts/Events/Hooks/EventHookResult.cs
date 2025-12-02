namespace BBT.Workflow.Events.Hooks;

/// <summary>
/// Represents the result of an event hook execution.
/// Contains success/failure status and optional metadata to be added to the event.
/// </summary>
/// <param name="IsSuccess">Indicates whether the hook executed successfully.</param>
/// <param name="ExtraMetadata">
/// Optional dictionary of additional metadata to be added to the event.
/// This metadata will be merged into the event's metadata collection.
/// </param>
/// <param name="Exception">
/// Optional exception that occurred during hook execution.
/// Only applicable when <paramref name="IsSuccess"/> is false.
/// </param>
/// <remarks>
/// <para>
/// Even if a hook fails (IsSuccess = false), the event will still be published.
/// The failure information will be logged and added to the event metadata.
/// </para>
/// <para>
/// Use the factory methods <see cref="Ok"/> and <see cref="Fail"/> for convenient creation.
/// </para>
/// </remarks>
public sealed record EventHookResult(
    bool IsSuccess,
    IDictionary<string, string>? ExtraMetadata = null,
    Exception? Exception = null)
{
    /// <summary>
    /// Creates a successful hook result.
    /// </summary>
    /// <param name="extra">Optional metadata to add to the event.</param>
    /// <returns>A successful <see cref="EventHookResult"/>.</returns>
    public static EventHookResult Ok(IDictionary<string, string>? extra = null)
        => new(true, extra);

    /// <summary>
    /// Creates a failed hook result with exception information.
    /// </summary>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <param name="extra">Optional metadata to add to the event.</param>
    /// <returns>A failed <see cref="EventHookResult"/>.</returns>
    public static EventHookResult Fail(Exception ex, IDictionary<string, string>? extra = null)
        => new(false, extra, ex);
}

