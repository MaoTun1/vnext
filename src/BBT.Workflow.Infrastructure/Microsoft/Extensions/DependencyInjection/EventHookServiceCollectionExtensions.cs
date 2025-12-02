using BBT.Workflow.Events.Hooks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering event hooks in the dependency injection container.
/// </summary>
public static class EventHookServiceCollectionExtensions
{
    /// <summary>
    /// Registers an event hook for the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event the hook handles. Must have <see cref="EventHookAttribute"/>.</typeparam>
    /// <typeparam name="THook">The hook implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEvent"/> does not have the <see cref="EventHookAttribute"/>.
    /// </exception>
    /// <remarks>
    /// The hook is registered with scoped lifetime. Use this method to register hooks
    /// that should be executed before events of type <typeparamref name="TEvent"/> are published.
    /// <para>
    /// The event type must be decorated with <see cref="EventHookAttribute"/>:
    /// <code>
    /// [EventHook]
    /// [EventName("my.event")]
    /// public class MyEvent : IDistributedEvent { }
    /// 
    /// services.AddEventHook&lt;MyEvent, MyEventHook&gt;();
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddEventHook<TEvent, THook>(this IServiceCollection services)
        where TEvent : class
        where THook : class, IEventPublishHook<TEvent>
    {
        ValidateEventHookAttribute<TEvent>();

        // Register the hook implementation
        services.AddScoped<IEventPublishHook<TEvent>, THook>();

        // Register the invoker wrapper for non-reflection invocation
        services.AddScoped<IEventHookInvoker>(sp =>
        {
            var hook = sp.GetRequiredService<IEventPublishHook<TEvent>>();
            return new EventHookInvoker<TEvent>(hook);
        });

        return services;
    }

    /// <summary>
    /// Registers an event hook for the specified event type with singleton lifetime.
    /// </summary>
    /// <typeparam name="TEvent">The type of event the hook handles. Must have <see cref="EventHookAttribute"/>.</typeparam>
    /// <typeparam name="THook">The hook implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// Use this for stateless hooks that don't depend on scoped services.
    /// </remarks>
    public static IServiceCollection AddEventHookSingleton<TEvent, THook>(this IServiceCollection services)
        where TEvent : class
        where THook : class, IEventPublishHook<TEvent>
    {
        ValidateEventHookAttribute<TEvent>();

        services.AddSingleton<IEventPublishHook<TEvent>, THook>();
        services.AddSingleton<IEventHookInvoker>(sp =>
        {
            var hook = sp.GetRequiredService<IEventPublishHook<TEvent>>();
            return new EventHookInvoker<TEvent>(hook);
        });

        return services;
    }

    /// <summary>
    /// Registers an event hook for the specified event type with transient lifetime.
    /// </summary>
    /// <typeparam name="TEvent">The type of event the hook handles. Must have <see cref="EventHookAttribute"/>.</typeparam>
    /// <typeparam name="THook">The hook implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddEventHookTransient<TEvent, THook>(this IServiceCollection services)
        where TEvent : class
        where THook : class, IEventPublishHook<TEvent>
    {
        ValidateEventHookAttribute<TEvent>();

        services.AddTransient<IEventPublishHook<TEvent>, THook>();
        services.AddTransient<IEventHookInvoker>(sp =>
        {
            var hook = sp.GetRequiredService<IEventPublishHook<TEvent>>();
            return new EventHookInvoker<TEvent>(hook);
        });

        return services;
    }

    /// <summary>
    /// Registers an event hook instance for the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event the hook handles. Must have <see cref="EventHookAttribute"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="hookInstance">The hook instance to register.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// Use this to register a pre-created hook instance as a singleton.
    /// </remarks>
    public static IServiceCollection AddEventHook<TEvent>(
        this IServiceCollection services,
        IEventPublishHook<TEvent> hookInstance)
        where TEvent : class
    {
        ValidateEventHookAttribute<TEvent>();

        services.AddSingleton(hookInstance);
        services.AddSingleton<IEventHookInvoker>(new EventHookInvoker<TEvent>(hookInstance));

        return services;
    }

    /// <summary>
    /// Registers an event hook using a factory function.
    /// </summary>
    /// <typeparam name="TEvent">The type of event the hook handles. Must have <see cref="EventHookAttribute"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">Factory function to create the hook instance.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddEventHook<TEvent>(
        this IServiceCollection services,
        Func<IServiceProvider, IEventPublishHook<TEvent>> factory)
        where TEvent : class
    {
        ValidateEventHookAttribute<TEvent>();

        services.AddScoped(factory);
        services.AddScoped<IEventHookInvoker>(sp =>
        {
            var hook = factory(sp);
            return new EventHookInvoker<TEvent>(hook);
        });

        return services;
    }

    /// <summary>
    /// Validates that the event type has the <see cref="EventHookAttribute"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type to validate.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the event type does not have the attribute.
    /// </exception>
    private static void ValidateEventHookAttribute<TEvent>() where TEvent : class
    {
        if (!Attribute.IsDefined(typeof(TEvent), typeof(EventHookAttribute)))
        {
            throw new InvalidOperationException(
                $"Event type '{typeof(TEvent).Name}' must be decorated with [{nameof(EventHookAttribute)}] " +
                "to register hooks. Add the attribute to enable hook support for this event type.");
        }
    }
}
