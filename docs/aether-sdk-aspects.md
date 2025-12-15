# Aether SDK Aspects

## Overview

The BBT Workflow Engine uses **Aether SDK Aspects** for cross-cutting concerns such as transaction management, logging, and distributed tracing. These aspects use PostSharp to weave behavior into methods at compile time, providing clean separation of concerns without cluttering business logic.

## Available Aspects

### 1. `[UnitOfWork]` Attribute

Manages database transactions automatically. Wraps the method execution in a transaction that commits on success and rolls back on failure.

```csharp
using BBT.Aether.Aspects;
using BBT.Aether.Uow;

public sealed class WorkflowExecutionService : IWorkflowExecutionService
{
    [UnitOfWork]
    public async Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // All database operations within this method are wrapped in a transaction
        // Transaction commits automatically on success
        // Transaction rolls back automatically on exception
        return await ExecuteInternalAsync(context, cancellationToken);
    }
}
```

#### UnitOfWork Options

When you need fine-grained control over transaction behavior:

```csharp
// Manual UnitOfWork with options
await using (var uow = await unitOfWorkManager.BeginAsync(new UnitOfWorkOptions
{
    Scope = UnitOfWorkScopeOption.RequiresNew,  // Always create new transaction
    IsolationLevel = IsolationLevel.ReadCommitted,
    IsTransactional = true
}, cancellationToken))
{
    // Perform database operations
    await repository.UpdateAsync(entity, cancellationToken);
    
    // Explicitly complete the unit of work
    await uow.CompleteAsync(cancellationToken);
}
```

#### UnitOfWork Scope Options

| Option | Description |
|--------|-------------|
| `Required` | Uses existing transaction if available, creates new if not (default) |
| `RequiresNew` | Always creates a new transaction |
| `Suppress` | Executes without a transaction |

### 2. `[AutoUnitOfWork]` Assembly Attribute

Automatically applies UnitOfWork behavior to all public methods in the assembly that match certain patterns. Configured at the assembly level:

```csharp
// In AssemblyInfo.cs or any .cs file
using BBT.Aether.Aspects;

[assembly: AutoUnitOfWork]
```

This assembly-level attribute automatically wraps qualifying methods with transaction management, reducing boilerplate code.

### 3. `[Log]` Attribute

Automatically logs method entry, exit, and exceptions using structured logging. Integrates with the configured logging provider (Serilog, OpenTelemetry, etc.).

```csharp
using BBT.Aether.Aspects;

public sealed class WorkflowExecutionService : IWorkflowExecutionService
{
    [Log]
    public async Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Automatically logs:
        // - Method entry with parameters
        // - Method exit with result
        // - Exceptions with stack trace
        return await ExecuteInternalAsync(context, cancellationToken);
    }
}
```

#### Log Output Example

```
[INF] Entering WorkflowExecutionService.ExecuteTransitionAsync
      Parameters: { context: { InstanceId: "abc123", TransitionKey: "approve" } }
[INF] Exiting WorkflowExecutionService.ExecuteTransitionAsync
      Duration: 145ms, Result: Success
```

### 4. `[Trace]` Attribute

Creates OpenTelemetry spans for distributed tracing. Each traced method becomes a span in the trace, enabling end-to-end visibility.

```csharp
using BBT.Aether.Aspects;
using System.Diagnostics;

public sealed class ChangeStateStep : ITransitionStep
{
    public int Order => LifecycleOrder.ChangeState;

    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Creates a span named "ChangeStateStep.ExecuteAsync"
        // Automatically includes:
        // - Start/end timestamps
        // - Method parameters as tags
        // - Exception details on failure
        
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ChangeStateStep)}");
        
        return await PerformStateChangeAsync(context, cancellationToken);
    }
}
```

#### Trace Integration with Activity

The `[Trace]` attribute works with `System.Diagnostics.Activity` for rich telemetry:

```csharp
[Trace]
public async Task<Result<TransitionExecutionContext>> ExecuteAsync(
    WorkflowExecutionContext context,
    CancellationToken cancellationToken)
{
    var activity = Activity.Current;
    
    // Enrich the span with custom tags
    activity?.SetTag(TelemetryConstants.TagNames.Domain, ctx.Workflow.Domain);
    activity?.SetTag(TelemetryConstants.TagNames.Flow, ctx.Workflow.Key);
    activity?.SetTag(TelemetryConstants.TagNames.InstanceId, ctx.InstanceId.ToString());
    
    // Set display name for trace visualization
    activity?.SetDisplayName($"{ctx.InstanceId}/{ctx.TransitionKey}");
    
    // Propagate context through baggage
    activity?.SetBaggage(TelemetryConstants.TagNames.Domain, ctx.Workflow.Domain);
    
    return await ExecuteInternalAsync(context, cancellationToken);
}
```

#### Setting Activity Status

```csharp
private static void SetActivityStatus<T>(Activity? activity, Result<T> result)
{
    if (activity is null) return;

    if (result.IsSuccess)
    {
        activity.SetStatus(ActivityStatusCode.Ok);
    }
    else
    {
        activity.SetStatus(ActivityStatusCode.Error, result.Error.Message);
        activity.AddTag("error.code", result.Error.Code);
    }
}
```

### 5. `[Enrich]` Parameter Attribute

Enriches log context with parameter values. Used in combination with `[Log]` to add contextual data to all logs within the method scope.

```csharp
using BBT.Aether.Aspects;

public sealed class WorkflowExecutionService : IWorkflowExecutionService
{
    [UnitOfWork]
    [Log]
    [Trace]
    public Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        [Enrich] WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // The [Enrich] attribute extracts properties from WorkflowExecutionContext
        // and adds them to the log scope:
        // - InstanceId
        // - TransitionKey
        // - Domain
        // - WorkflowKey
        // All logs within this method will include these properties
        
        return ExecuteInternalAsync(context, cancellationToken);
    }
}
```

## Combining Aspects

Aspects can be combined for comprehensive cross-cutting behavior:

```csharp
public sealed class WorkflowExecutionService : IWorkflowExecutionService
{
    /// <summary>
    /// Executes a workflow transition using Railway Programming pattern.
    /// </summary>
    [UnitOfWork]  // 1. Transaction management
    [Log]         // 2. Structured logging
    [Trace]       // 3. Distributed tracing
    public Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        [Enrich] WorkflowExecutionContext context,  // 4. Log enrichment
        CancellationToken cancellationToken = default)
    {
        return GetExecutionStrategy(context.Mode)
            .BindAsync(strategy => ExecuteStrategyAsync(strategy, context, cancellationToken))
            .BindAsync(execCtx => BuildTransitionOutputAsync(context, execCtx, cancellationToken));
    }
}
```

### Execution Order

When multiple aspects are applied, they execute in the following order:

1. **[UnitOfWork]** - Outermost, manages transaction boundary
2. **[Log]** - Logs entry/exit within transaction
3. **[Trace]** - Creates span within log scope
4. **Method Body** - Actual business logic

```
┌─────────────────────────────────────────────┐
│  [UnitOfWork] - Begin Transaction           │
│  ┌─────────────────────────────────────────┐│
│  │  [Log] - Log Entry                      ││
│  │  ┌─────────────────────────────────────┐││
│  │  │  [Trace] - Start Span               │││
│  │  │  ┌─────────────────────────────────┐│││
│  │  │  │  Method Body                    ││││
│  │  │  └─────────────────────────────────┘│││
│  │  │  [Trace] - End Span                 │││
│  │  └─────────────────────────────────────┘││
│  │  [Log] - Log Exit                       ││
│  └─────────────────────────────────────────┘│
│  [UnitOfWork] - Commit/Rollback             │
└─────────────────────────────────────────────┘
```

## Pipeline Steps with Trace

All transition pipeline steps use `[Trace]` for observability:

```csharp
public sealed class RunOnExecuteTasksStep : ITransitionStep
{
    public int Order => LifecycleOrder.OnExecute;

    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(RunOnExecuteTasksStep)}");
        
        // Execute OnExecute tasks...
        return Result<StepOutcome>.Ok(StepOutcome.Continue());
    }
}
```

### Pipeline Trace Visualization

```
Transition Execution
├── SyncTransitionStrategy.ExecuteAsync
│   ├── [5] ForwardToActiveSubflowStep
│   ├── [10] CreateTransitionRecordStep
│   ├── [20] RunOnExecuteTasksStep
│   │   └── TaskCoordinator.ExecuteAsync
│   ├── [30] RunOnExitTasksStep
│   ├── [40] ChangeStateStep
│   ├── [50] RunOnEntryTasksStep
│   ├── [60] HandleSubFlowStep
│   ├── [70] ScheduleTransitionsStep
│   ├── [80] RunAutomaticTransitionsStep
│   ├── [90] HandleFinishStep
│   └── [100] FinalizeTransitionStep
```

## PostSharp Integration

Aether SDK aspects are built on PostSharp. The integration is configured in project files:

```xml
<!-- In .csproj file -->
<ItemGroup>
    <PackageReference Include="BBT.Aether.Aspects" Version="x.x.x" />
</ItemGroup>
```

PostSharp weaves aspect behavior at compile time, ensuring:
- Zero runtime reflection overhead
- Full IntelliSense support
- Compile-time validation

## Best Practices

### 1. Use [Trace] for All Pipeline Steps

```csharp
// Good: Every step is traced
[Trace]
public async Task<Result<StepOutcome>> ExecuteAsync(
    TransitionExecutionContext context,
    CancellationToken cancellationToken)
{
    Activity.Current?.SetDisplayName($"[{Order}] {GetType().Name}");
    // ...
}
```

### 2. Combine Aspects for Service Methods

```csharp
// Good: Full observability stack
[UnitOfWork]
[Log]
[Trace]
public async Task<Result<T>> ExecuteAsync(...)
```

### 3. Use [Enrich] for Context Parameters

```csharp
// Good: Context properties flow to all logs
public Task<Result<T>> ProcessAsync(
    [Enrich] RequestContext context,
    CancellationToken cancellationToken)
```

### 4. Set Meaningful Display Names

```csharp
// Good: Clear span names in traces
Activity.Current?.SetDisplayName($"[{Order}] {nameof(ChangeStateStep)}");
Activity.Current?.SetDisplayName($"{instanceId}/{transitionKey}");
```

### 5. Enrich Traces with Domain Data

```csharp
// Good: Rich telemetry context
activity?.SetTag(TelemetryConstants.TagNames.Domain, ctx.Workflow.Domain);
activity?.SetTag(TelemetryConstants.TagNames.Flow, ctx.Workflow.Key);
activity?.SetTag(TelemetryConstants.TagNames.InstanceId, ctx.InstanceId.ToString());
```

## Configuration

### Telemetry Constants

Define consistent tag names across the application:

```csharp
public static class TelemetryConstants
{
    public static class TagNames
    {
        public const string Domain = "workflow.domain";
        public const string Flow = "workflow.flow";
        public const string FlowVersion = "workflow.version";
        public const string InstanceId = "workflow.instance.id";
        public const string TransitionKey = "workflow.transition.key";
        public const string TriggerType = "workflow.trigger.type";
        public const string HandlerName = "workflow.handler.name";
    }
}
```

## Related Documentation

- [Result Pattern & Railway Programming](./result-pattern-railway.md) - Error handling with Result pattern
- [OpenTelemetry Logging](./opentelemetry-logging.md) - Distributed tracing configuration
- [Transition Pipeline Architecture](./transition-pipeline-architecture.md) - Pipeline step implementation

