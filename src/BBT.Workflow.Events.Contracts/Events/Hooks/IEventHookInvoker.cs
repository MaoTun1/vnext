namespace BBT.Workflow.Events.Hooks;

/// <summary>
/// Non-generic interface for invoking event hooks.
/// Used internally by the event bus to invoke hooks without reflection.
/// </summary>
/// <remarks>
/// This interface enables type-safe hook invocation without runtime reflection.
/// Each <see cref="IEventPublishHook{TEvent}"/> is wrapped by an <see cref="EventHookInvoker{TEvent}"/>
/// that implements this interface.
/// </remarks>
public interface IEventHookInvoker
{
    /// <summary>
    /// Gets the event type this invoker handles.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Gets the name of the hook for logging purposes.
    /// </summary>
    string HookName { get; }

    /// <summary>
    /// Invokes the hook with the specified event data.
    /// </summary>
    /// <param name="eventData">The event data (must be of the correct type).</param>
    /// <param name="context">The hook context with metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook execution result.</returns>
    Task<EventHookResult> InvokeAsync(
        object eventData,
        EventHookContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic invoker that wraps an <see cref="IEventPublishHook{TEvent}"/> and provides
/// type-safe invocation without reflection.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
/// <remarks>
/// This class is registered in DI automatically when using <c>AddEventHook</c>.
/// It casts the event data once and calls the strongly-typed hook method directly.
/// </remarks>
public sealed class EventHookInvoker<TEvent> : IEventHookInvoker
    where TEvent : class
{
    private readonly IEventPublishHook<TEvent> _hook;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventHookInvoker{TEvent}"/> class.
    /// </summary>
    /// <param name="hook">The hook implementation to wrap.</param>
    public EventHookInvoker(IEventPublishHook<TEvent> hook)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
    }

    /// <inheritdoc />
    public Type EventType => typeof(TEvent);

    /// <inheritdoc />
    public string HookName => _hook.GetType().Name;

    /// <inheritdoc />
    public Task<EventHookResult> InvokeAsync(
        object eventData,
        EventHookContext context,
        CancellationToken cancellationToken = default)
    {
        if (eventData is not TEvent typedEvent)
        {
            throw new ArgumentException(
                $"Event data must be of type {typeof(TEvent).Name}, but was {eventData?.GetType().Name ?? "null"}",
                nameof(eventData));
        }

        return _hook.BeforePublishAsync(typedEvent, context, cancellationToken);
    }
}

