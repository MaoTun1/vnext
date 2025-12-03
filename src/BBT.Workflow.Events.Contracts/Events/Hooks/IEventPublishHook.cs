namespace BBT.Workflow.Events.Hooks;

/// <summary>
/// Defines a strongly-typed hook that is executed before an event of type <typeparamref name="TEvent"/> is published.
/// This generic version provides type-safe access to the event data without casting.
/// </summary>
/// <typeparam name="TEvent">The type of event this hook handles.</typeparam>
/// <remarks>
/// <para>
/// Implementations of this interface should be registered in the dependency injection container
/// using <c>services.AddScoped&lt;IEventPublishHook&lt;TEvent&gt;, MyHook&gt;()</c>.
/// The hook will automatically be discovered and executed when events of type <typeparamref name="TEvent"/> are published.
/// </para>
/// <para>
/// Hook execution failures do not prevent event publishing. If a hook throws an exception
/// or returns a failed result, the error will be logged and added to the event metadata,
/// but the event will still be published.
/// </para>
/// </remarks>
public interface IEventPublishHook<in TEvent> where TEvent : class
{
    /// <summary>
    /// Executes the hook logic before an event is published.
    /// </summary>
    /// <param name="eventData">The strongly-typed event data being published.</param>
    /// <param name="context">The context containing event metadata and additional information.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the hook execution result with optional metadata.
    /// </returns>
    /// <remarks>
    /// This method should not throw exceptions. If an error occurs, catch it and return
    /// <see cref="EventHookResult.Fail"/> with the exception information.
    /// </remarks>
    Task<EventHookResult> BeforePublishAsync(
        TEvent eventData,
        EventHookContext context,
        CancellationToken cancellationToken = default);
}

