# EventBus Hooks

## Overview

The EventBus Hooks feature provides a way to execute custom logic before domain events are published, without modifying the domain layer code. This is useful for scenarios such as:

- Synchronizing parent workflows when sub-workflows complete
- Enriching events with additional metadata
- Validating event data before publishing
- Performing side effects (e.g., logging, notifications)

## Architecture

The implementation follows the **Decorator Pattern** with **Generic Interface** for type safety:

### Events.Contracts Layer (`BBT.Workflow.Events.Contracts`)
Contains hook contracts and event definitions:
- `IEventPublishHook<TEvent>` - **Generic** interface for implementing type-safe hooks
- `EventHookContext` - Context passed to hooks with metadata
- `EventHookResult` - Result returned by hooks

### Application Layer (`BBT.Workflow.Application`)
Contains hook implementations:
- Business logic hooks that process specific event types
- Example: `InstanceSubCompletedEventHook`

### Infrastructure Layer (`BBT.Workflow.Infrastructure`)
Contains:
- `HookedDistributedEventBus` - Decorator that wraps Aether's `IDistributedEventBus`
- `EventBusHookServiceCollectionExtensions` - DI registration extensions
- `EventHookServiceCollectionExtensions` - Hook registration helpers

### Integration
The wrapper is automatically registered in `WorkflowApiBaseServiceCollectionExtensions` via `AddEventBusWithHooks()`.

## How It Works

1. **Hook Registration**: Hooks are registered in DI using `services.AddEventHook<TEvent, THook>()`
2. **Hook Discovery**: When an event is published, the `HookedDistributedEventBus` resolves all `IEventPublishHook<TEvent>` via `IEventHookInvoker`
3. **Execution**: Each hook's `BeforePublishAsync` method is called with strongly-typed event data
4. **Metadata Enrichment**: Hook results can add metadata to the event
5. **Publishing Decision**:
   - If **all hooks succeed**: Event is considered "handled" and is NOT published to the inner bus
   - If **any hook fails** or no hooks exist: Event is published to the inner bus as fallback

### Key Behaviors

- **Type-Safe**: Generic `IEventPublishHook<TEvent>` interface provides compile-time type checking
- **No Circular Dependencies**: Hook registration via DI eliminates cross-project dependencies
- **Hook Success = Event Handled**: When all hooks succeed, the event is considered fully processed
- **Fallback Publishing**: On hook failure, events are published to inner bus for reliability
- **Error Handling**: All exceptions are caught and logged with structured metadata
- **Invoker Pattern**: `EventHookInvoker<TEvent>` wraps hooks for runtime type resolution

## Usage

### Step 1: Create a Hook Implementation

Create a hook class using the generic `IEventPublishHook<TEvent>` interface:

```csharp
using BBT.Workflow.Events.Hooks;
using BBT.Workflow.Instances.Events;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.Instances.Events;

/// <summary>
/// Hook that processes InstanceSubCompletedEvent before publishing.
/// When successful, the event is considered handled and won't be published to the bus.
/// </summary>
public sealed class InstanceSubCompletedEventHook(
    ISubflowCompletionService subflowCompletionService,
    ICurrentSchema currentSchema,
    IUnitOfWorkManager unitOfWorkManager) : IEventPublishHook<InstanceSubCompletedEvent>
{
    public async Task<EventHookResult> BeforePublishAsync(
        EventHookContext context,
        CancellationToken cancellationToken = default)
    {
        // Context.EventData is already strongly-typed to InstanceSubCompletedEvent
        var evt = (InstanceSubCompletedEvent)context.EventData;

        try
        {
            // Switch to appropriate schema
            using (currentSchema.Use(evt.Flow))
            {
                // Begin new unit of work for the completion handling
                await using (var uow = await unitOfWorkManager.BeginAsync(
                    new UnitOfWorkOptions { Scope = UnitOfWorkScopeOption.RequiresNew },
                    cancellationToken))
                {
                    var completedData = new FlowCompletedInput
                    {
                        SubInstanceId = evt.SubInstanceId,
                        InstanceId = evt.InstanceId,
                        Domain = evt.Domain,
                        Flow = evt.Flow,
                        CompletedState = evt.CompletedState
                    };

                    await subflowCompletionService.CompletionAsync(completedData, cancellationToken);
                    await uow.CompleteAsync(cancellationToken);
                }
            }

            // Success - event is handled, won't be published to bus
            return EventHookResult.Ok(new Dictionary<string, string>
            {
                ["hook_status"] = "completed",
                ["processed_at"] = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            // Failure - event will be published to bus as fallback
            return EventHookResult.Fail(ex, new Dictionary<string, string>
            {
                ["hook_status"] = "failed"
            });
        }
    }
}
```

### Step 2: Register the Hook in DI

Register your hook using the generic extension method:

```csharp
// In WorkflowInfrastructureModuleServiceCollectionExtensions.cs
public static IServiceCollection AddInfrastructureModule(
    this IServiceCollection services)
{
    // ... existing registrations ...

    // Register event hooks using generic method
    services.AddEventHook<InstanceSubCompletedEvent, InstanceSubCompletedEventHook>();
    services.AddEventHook<InstanceCanceledEvent, InstanceCanceledEventHook>();
    
    // Alternative: Register with specific lifetime
    services.AddEventHookSingleton<AuditEvent, AuditEventHook>();  // Singleton
    services.AddEventHookTransient<MetricEvent, MetricEventHook>(); // Transient

    return services;
}
```

The registration helper automatically:
1. Registers the hook as `IEventPublishHook<TEvent>`
2. Creates an `EventHookInvoker<TEvent>` wrapper for runtime resolution

### Step 3: Decorate Events with Hook Attributes

Add the `[EventHook]` attribute to your event class:

```csharp
using BBT.Aether.Events;
using BBT.Workflow.Events.Hooks;

namespace BBT.Workflow.Instances.Events;

[EventName("instance.sub.completed")]
[EventHook(typeof(ParentFlowSyncEventHook))]
public class InstanceSubCompletedEvent : IDistributedEvent
{
    [EventSubject]
    public required Guid InstanceId { get; init; }
    public required Guid SubInstanceId { get; init; }
    public required string CompletedState { get; init; }
    // ... other properties
}
```

### Step 4: Raise Events Normally

No changes needed in domain code! Continue using `AddDistributedEvent()`:

```csharp
public class Instance : AggregateRoot<Guid>
{
    public void CompleteSubFlow()
    {
        // Update domain state
        Status = InstanceStatus.Completed;

        // Add event - hooks will execute automatically
        AddDistributedEvent(new InstanceSubCompletedEvent
        {
            InstanceId = ParentInstanceId,
            SubInstanceId = Id,
            CompletedState = CurrentState
        });
    }
}
```

## Multiple Hooks

You can attach multiple hooks to a single event:

```csharp
[EventName("instance.sub.completed")]
[EventHook(typeof(ParentFlowSyncEventHook))]
[EventHook(typeof(AuditLogEventHook))]
[EventHook(typeof(MetricsEventHook))]
public class InstanceSubCompletedEvent : IDistributedEvent
{
    // ...
}
```

Hooks execute in the order they are declared.

## Hook Context

The `EventHookContext` provides:

- `EventData` - The actual event object
- `EventType` - Fully qualified type name
- `Topic` - The event topic/subject
- `Metadata` - Dictionary of existing metadata

## Hook Results

Hooks return `EventHookResult`:

```csharp
// Success with optional metadata
return EventHookResult.Ok(new Dictionary<string, string>
{
    ["status"] = "processed"
});

// Failure with exception and optional metadata
return EventHookResult.Fail(exception, new Dictionary<string, string>
{
    ["status"] = "failed"
});
```

## Error Handling

### Hook Resolution Failures

If a hook cannot be resolved from DI:
- A warning is logged
- Error metadata is added: `hook_error_{HookName} = "HookNotResolved"`
- Processing continues with remaining hooks

### Hook Execution Failures

If a hook throws an exception or returns a failed result:
- The error is logged
- Error metadata is added:
  - `hook_error_{HookName}` = exception type name
  - `hook_error_{HookName}_message` = exception message
- The event is still published

### No Impact on Publishing

**Critical**: Hook failures never prevent event publishing. This ensures:
- System reliability
- Event delivery guarantees
- Graceful degradation

## Best Practices

### 1. Keep Hooks Focused

Each hook should have a single responsibility:

```csharp
// ✅ Good - focused responsibility
public class ParentFlowSyncEventHook : IEventPublishHook { }

// ❌ Bad - too many responsibilities
public class EverythingHook : IEventPublishHook { }
```

### 2. Handle Errors Gracefully

Always catch exceptions and return `EventHookResult.Fail()`:

```csharp
public async Task<EventHookResult> BeforePublishAsync(
    EventHookContext context,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Hook logic
        return EventHookResult.Ok();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Hook failed");
        return EventHookResult.Fail(ex);
    }
}
```

### 3. Type Check Event Data

Check the event type before processing:

```csharp
if (context.EventData is not MyEventType evt)
{
    return EventHookResult.Ok(); // Not applicable
}
```

### 4. Use Appropriate Lifetimes

Register hooks with appropriate DI lifetimes:
- `Scoped` - For hooks that depend on per-request services
- `Singleton` - For stateless hooks

### 5. Add Meaningful Metadata

Use metadata to communicate hook status:

```csharp
return EventHookResult.Ok(new Dictionary<string, string>
{
    ["sync_status"] = "ok",
    ["sync_duration_ms"] = stopwatch.ElapsedMilliseconds.ToString(),
    ["sync_timestamp"] = DateTime.UtcNow.ToString("O")
});
```

## Testing

### Unit Testing Hooks

```csharp
[Fact]
public async Task BeforePublishAsync_ShouldReturnOk_WhenSyncSucceeds()
{
    // Arrange
    var syncService = Substitute.For<IParentFlowSyncService>();
    var logger = Substitute.For<ILogger<ParentFlowSyncEventHook>>();
    var hook = new ParentFlowSyncEventHook(syncService, logger);

    var evt = new InstanceSubCompletedEvent
    {
        InstanceId = Guid.NewGuid(),
        SubInstanceId = Guid.NewGuid(),
        CompletedState = "final"
    };

    var context = new EventHookContext(
        evt,
        evt.GetType().FullName!,
        "instance.sub.completed",
        new Dictionary<string, string>());

    // Act
    var result = await hook.BeforePublishAsync(context);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.ExtraMetadata.ShouldNotBeNull();
    result.ExtraMetadata["parent_sync_status"].ShouldBe("ok");
}
```

### Integration Testing

Test that hooks execute during event publishing:

```csharp
[Fact]
public async Task SaveChangesAsync_ShouldExecuteHooks_WhenEventsArePublished()
{
    // Arrange
    var instance = await CreateInstanceAsync();
    instance.CompleteSubFlow();

    // Act
    await _dbContext.SaveChangesAsync();

    // Assert
    // Verify hook was called (using test doubles or monitoring)
}
```

## Troubleshooting

### Hook Not Executing

1. Verify hook is registered in DI container
2. Check event has `[EventHook(typeof(MyHook))]` attribute
3. Review logs for resolution errors

### Hook Failing Silently

1. Check application logs for hook execution errors
2. Verify hook returns `EventHookResult.Fail()` instead of throwing
3. Add breakpoints in hook implementation

### Metadata Not Added

1. Ensure hook returns metadata in `EventHookResult`
2. Verify event type supports metadata (has Extensions or Metadata property)
3. Check logs for enrichment messages

## Performance Considerations

- **Hook Execution**: Hooks execute synchronously before publishing
- **Async Operations**: Use async/await for I/O operations
- **Timeout**: Consider implementing timeouts for long-running hooks
- **Caching**: Cache frequently accessed data in hooks
- **Monitoring**: Monitor hook execution time and failures

## HookedDistributedEventBus

The `HookedDistributedEventBus` is a decorator that wraps Aether's `IDistributedEventBus`:

```csharp
public sealed class HookedDistributedEventBus : IDistributedEventBus
{
    private readonly IDistributedEventBus _inner;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HookedDistributedEventBus> _logger;

    public async Task PublishAsync<TEvent>(
        TEvent payload,
        string? subject = null,
        bool useOutbox = true,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        // Execute hooks
        var hookResult = await ExecuteHooksAsync(payload, cancellationToken);

        // If all hooks succeeded, event is handled - don't publish
        if (hookResult.HooksExecuted && hookResult.AllSucceeded)
        {
            return;
        }

        // If no hooks or any hook failed, publish to inner bus as fallback
        if (hookResult.HasFailures)
        {
            _logger.LogWarning(
                "Hook(s) failed for event {EventType}, publishing to inner bus as fallback",
                payload.GetType().Name);
        }

        await _inner.PublishAsync(payload, subject, useOutbox, cancellationToken);
    }
}
```

### Hook Execution Flow

```
Event Published
      │
      ▼
┌─────────────────────────────────┐
│ HookedDistributedEventBus       │
│ ┌─────────────────────────────┐ │
│ │ Check [EventHook] attribute │ │
│ └─────────────────────────────┘ │
│           │                      │
│           ▼                      │
│ ┌─────────────────────────────┐ │
│ │ Resolve IEventHookInvoker   │ │
│ │ for each hook type          │ │
│ └─────────────────────────────┘ │
│           │                      │
│           ▼                      │
│ ┌─────────────────────────────┐ │
│ │ Execute all hooks           │ │
│ └─────────────────────────────┘ │
│           │                      │
│     ┌─────┴─────┐               │
│     ▼           ▼               │
│ All Success    Any Failure      │
│     │           │               │
│     ▼           ▼               │
│  HANDLED    Publish to Inner    │
│ (No Publish)     Bus            │
└─────────────────────────────────┘
```

## Related Documentation

- [Architecture Overview](./architecture-overview.md) - Event-driven architecture
- [Inbox/Outbox Workers](./inbox-outbox-workers.md) - Event processing workers
- [Aether SDK Aspects](./aether-sdk-aspects.md) - Cross-cutting concerns

