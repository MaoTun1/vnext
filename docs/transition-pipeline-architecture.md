# Transition Pipeline Architecture

This documentation describes the new **Transition Pipeline Architecture** refactoring implemented in the BBT.Workflow project.

## Overview

The new architecture is designed to make transition execution **readable, testable, and extensible**. It reduces complexity on the `StateMachineExecutor` and defines Auto/Schedule **re-entry** scenarios as first-class citizens.

## Core Principles

- **SRP & Separation of Concerns:** Sync/Async mode selection ≠ Trigger type management ≠ Lifecycle step execution
- **Deterministic Lifecycle:** Well-defined and documented sequence
- **Context Rehydration:** Context is not carried over in Auto/Schedule re-entry; it is rebuilt in a new DI scope
- **No Service Locator:** Services are not in Context but injected into steps/handlers via DI
- **Idempotency & Lock:** Instance-based locking and idempotency is a core feature

## Architecture Components

### 1. Core Interfaces

#### TriggerType Enum
```csharp
public enum TriggerType
{
    Manual = 0,     // User-triggered
    Automatic = 1,  // System-triggered automatically
    Scheduled = 2,  // Timer-triggered
    Event = 3       // Externally triggered by event
}
```

#### ITransitionStep
```csharp
public interface ITransitionStep
{
    int Order { get; }
    Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}
```

**Note:** Pipeline steps use the Result pattern, returning `Result<StepOutcome>` for exception-free error handling and flow control.

#### ITransitionHandler
```csharp
public interface ITransitionHandler
{
    bool CanHandle(TriggerType triggerType);
    Task PreHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
    Task PostHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}
```

### 2. Pipeline Steps (Lifecycle Order)

All steps implement `ITransitionStep` and return `Result<StepOutcome>`:

1. **ForwardToActiveSubflowStep** (Order: 5) - Forwards transition to active subflow if exists
2. **CreateTransitionRecordStep** (Order: 10) - Creates the transition record
3. **RunOnExecuteTasksStep** (Order: 20) - Executes the transition's OnExecute tasks
4. **RunOnExitTasksStep** (Order: 30) - Executes the current state's OnExit tasks
5. **ChangeStateStep** (Order: 40) - Performs the state change
6. **RunOnEntryTasksStep** (Order: 50) - Executes the target state's OnEntry tasks
7. **HandleSubFlowStep** (Order: 60) - Manages SubFlow operations
8. **ClearBusyOnResumeStep** (Order: 69) - Clears BUSY status on resume
9. **ScheduleTransitionsStep** (Order: 70) - Schedules timed transitions
10. **RunAutomaticTransitionsStep** (Order: 80) - Enqueues automatic transitions for execution
11. **HandleFinishStep** (Order: 90) - Handles workflow completion
12. **FinalizeTransitionStep** (Order: 100) - Finalizes the transition and performs cleanup
13. **ProcessInlineAutoChainStep** (Order: 101) - Processes inline automatic transition chain

### 3. Trigger Handlers

#### ManualTransitionHandler
- Policy/HMAC/Auth/Schema validation
- User authorization
- Audit logging

#### AutomaticTransitionHandler
- Condition re-validation
- Chain depth control
- Execution metrics

#### ScheduledTransitionHandler
- Timing validation
- Schedule constraints
- Recurring schedule management

#### EventTransitionHandler
- Event source validation
- Event correlation
- Payload validation

### 4. Re-entry System

#### ReentryCommand
```csharp
public sealed record ReentryCommand(
    Guid InstanceId,
    string Domain,
    string WorkflowKey,
    string TransitionKey,
    TriggerType TriggerType,
    string? Actor = null,
    string? ExecutionChainId = null,
    int ChainDepth = 0,
    bool PreferInline = false,
    IReadOnlyDictionary<string,string>? Headers = null);
```

#### IReentryDispatcher
- `DispatchAutoAsync`: Manages automatic transitions (inline or background job)
- `DispatchScheduledAsync`: Queues scheduled transitions as background jobs

### 5. TransitionPipeline

The pipeline orchestrates the execution of transition lifecycle steps in a deterministic order.

#### Core Responsibilities
- Orders all registered pipeline steps by their `Order` property
- Executes steps sequentially with proper error handling
- Manages dynamic re-planning based on directive changes
- Provides structured logging and distributed tracing for each step
- Returns `Result` to indicate success or failure without throwing exceptions

#### Execution Plan Building
The pipeline dynamically builds execution plans using the `BuildExecutionPlan` method:

```csharp
private IReadOnlyList<ITransitionStep> BuildExecutionPlan(TransitionExecutionContext context)
{
    var ordered = _steps.ToList(); // Already ordered in constructor

    // 1) ResumeFrom: Start from a specific step
    var startOrder = context.Directives.ConsumeResumeFrom();
    if (startOrder.HasValue)
        ordered = ordered.Where(s => s.Order >= startOrder.Value).ToList();

    // 2) Terminal State: Short-circuit to Finalize
    if (context.Directives.TerminalReached)
    {
        var maxOrder = LifecycleOrder.Finalize;
        ordered = ordered.Where(s => s.Order <= maxOrder).ToList();
    }

    // 3) Epilogue Mode: Skip or run Schedule/Auto steps
    if (context.Directives.Epilogue == EpilogueMode.Skip)
    {
        ordered = ordered
            .Where(s => s.Order != LifecycleOrder.Schedule &&
                        s.Order != LifecycleOrder.Auto)
            .ToList();
    }

    return ordered;
}
```

#### Planning Strategies
- **ResumeFrom**: Start from a specific step (subflow completion, re-planning)
- **Terminal State**: Run only up to Finalize in terminal state
- **Epilogue Mode**: Skip or run Schedule/Auto steps based on directives
- **Dynamic Re-planning**: Rebuild plan when directives change mid-execution

### 6. LifecycleOrder

Constants defining the standard execution order for transition lifecycle steps:

```csharp
public static class LifecycleOrder
{
    public const int Preflight = 5;           // Subflow preflight check
    public const int CreateTransition = 10;   // Create transition record
    public const int OnExecute = 20;          // Execute transition's OnExecute tasks
    public const int OnExit = 30;             // Execute current state's OnExit tasks
    public const int ChangeState = 40;        // Change the instance state
    public const int OnEntry = 50;            // Execute target state's OnEntry tasks
    public const int SubFlow = 60;            // Handle SubFlow operations
    public const int ClearBusyOnResumeStep = Schedule - 1; // Clear BUSY status
    public const int Schedule = 70;           // Schedule future transitions
    public const int Auto = 80;               // Execute automatic transitions
    public const int Finish = 90;             // Handle workflow finishing
    public const int Finalize = 100;          // Finalize transition and cleanup
    public const int AfterEpilogueRefresh = Finalize + 1; // Process inline auto chain
}
```

### 7. PipelineDirectives

Directive system controlling pipeline behavior:

```csharp
public sealed class PipelineDirectives
{
    // Which step to resume from
    public int? ResumeFromOrder { get; private set; }
    
    // Behavior of epilogue (Schedule/Auto) steps
    public EpilogueMode Epilogue { get; private set; }
    
    // Inline automatic transition queue
    public Queue<ReentryCommand> InlineAutoQueue { get; }
    
    // Terminal state flag
    public bool TerminalReached { get; private set; }
    
    // SubFlow resume scenario
    public bool IsSubFlowResume { get; private set; }
}
```

#### EpilogueMode
```csharp
public enum EpilogueMode
{
    Run = 0,          // Run Schedule and Auto steps normally
    Skip = 1,         // Skip Schedule and Auto steps
    DispatchOnly = 2  // Only dispatch, don't execute
}
```

### 8. StepOutcome

Pipeline step result reporting and flow control. Steps return `Result<StepOutcome>` using the Result pattern:

```csharp
public sealed class StepOutcome
{
    public bool StopPipeline { get; init; }
    public int? SkipToOrder { get; init; }
    public string? NextTransitionKey { get; init; }
    public Action<PipelineDirectives>? MutateDirectives { get; init; }
    
    // Factory methods
    public static StepOutcome Continue();
    public static StepOutcome Stop();
    public static StepOutcome SkipTo(int order);
    public static StepOutcome With(Action<PipelineDirectives> fx);
}
```

**Usage Examples:**
```csharp
// Normal continuation
return Result<StepOutcome>.Ok(StepOutcome.Continue());

// Stop the pipeline
return Result<StepOutcome>.Ok(StepOutcome.Stop());

// Skip to a specific step (e.g., return to CreateTransition after inline auto)
return Result<StepOutcome>.Ok(StepOutcome.SkipTo(LifecycleOrder.CreateTransition));

// Mutate directives
return Result<StepOutcome>.Ok(StepOutcome.With(d => d.RequestEpilogue(EpilogueMode.Skip)));

// Error case
return Result<StepOutcome>.Fail(Error.Validation("step.failed", "Validation failed"));
```

### 9. Dynamic Re-planning

The pipeline detects directive changes during execution and rebuilds the plan dynamically:

**Re-planning Scenarios:**
1. **SubFlow Initiation**: Switch to epilogue SKIP mode
2. **Inline Auto Chain**: Restart from CreateTransition
3. **SubFlow Completion**: Resume from Schedule step
4. **Terminal State**: Execute only up to Finalize

**Pipeline Re-planning Flow:**
```csharp
public async Task<Result> RunAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
{
    // Build initial execution plan
    var plan = BuildExecutionPlan(context);
    var i = 0;
    
    while (i < plan.Count)
    {
        if (context.SkipImmediateExecution) break;

        var step = plan[i];
        
        // Execute step and handle Result
        var outcomeResult = await step.ExecuteAsync(context, cancellationToken);
        if (!outcomeResult.IsSuccess)
            return Result.Fail(outcomeResult.Error);
        
        var outcome = outcomeResult.Value!;
        
        // Apply directive mutations
        outcome.MutateDirectives?.Invoke(context.Directives);

        // 1) Stop pipeline?
        if (outcome.StopPipeline) break;

        // 2) SkipTo? (e.g., restart from CreateTransition after inline auto)
        if (outcome.SkipToOrder is { } skipTo)
        {
            context.Directives.RequestResumeFrom(skipTo);
            plan = BuildExecutionPlan(context); // Rebuild plan
            i = 0;
            continue;
        }

        // 3) If directives changed, update the plan
        if (NeedsReplan(plan, context.Directives))
        {
            context.Directives.RequestResumeFrom(step.Order + 1);
            plan = BuildExecutionPlan(context); // Rebuild plan
            i = 0;
            continue;
        }

        i++; // Move to next step
    }
    
    return Result.Ok();
}
```

**Re-planning Logic:**
```csharp
private static bool NeedsReplan(IReadOnlyList<ITransitionStep> currentPlan, PipelineDirectives d)
{
    // Replan if terminal state is reached
    if (d.TerminalReached) return true;
    
    // Replan if epilogue mode changed to SKIP and Schedule/Auto steps are in plan
    if (d.Epilogue == EpilogueMode.Skip &&
        currentPlan.Any(s => s.Order == LifecycleOrder.Schedule || s.Order == LifecycleOrder.Auto))
        return true;
    
    // Replan if resume-from is requested
    if (d.ResumeFromOrder is not null) return true;
    
    return false;
}
```

### 10. TransitionExecutionContext

Minimal, service-free context structure that carries only essential data and state:

```csharp
public sealed class TransitionExecutionContext
{
    // Identity (immutable)
    public string Domain { get; init; }
    public Guid InstanceId { get; init; }
    public string WorkflowKey { get; init; }
    public string TransitionKey { get; init; }
    public TriggerType Trigger { get; init; }
    public ExecutionActor Actor { get; set; }
    
    // Correlation and tracing
    public string CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string ExecutionChainId { get; init; }
    public int ChainDepth { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public string TraceId { get; init; }
    public string SpanId { get; init; }
    
    // Definitions (rehydrated)
    public Definitions.Workflow Workflow { get; init; }
    public State Current { get; set; }
    public State? Target { get; set; }
    public Transition? Transition { get; init; }
    
    // Instance snapshot
    public Instance Instance { get; set; }
    public string ConcurrencyToken { get; set; }
    public object? Data { get; set; }
    
    // Execution flags
    public bool SkipImmediateExecution { get; set; }
    public bool IsReentry { get; init; }
    
    // Headers and route values
    public IReadOnlyDictionary<string, string?> Headers { get; init; }
    public IReadOnlyDictionary<string, string?> RouteValues { get; init; }
    
    // Temporary storage
    public IDictionary<string, object?> Items { get; }
    public IDictionary<string, object?> Cache { get; }
    
    // Pipeline directives
    public PipelineDirectives Directives { get; }
    
    // Client response
    public ClientResponse? ClientResponse { get; set; }
    
    // Helper methods
    public ScriptContext GetOrBuildScriptContext(Func<ScriptContext> factory);
    public void ApplyScriptContextChanges(ScriptContext scriptContext);
    public void ClearCacheForFinalize();
}
```

**Key Design Principles:**
- **Service-Free**: Services are injected into steps/handlers, not stored in context
- **Minimal**: Contains only essential data needed across pipeline steps
- **Rehydrated**: Definitions and instance state are fetched fresh for each execution
- **Items vs Cache**: 
  - `Items`: Temporary storage shared between steps (cleared after execution)
  - `Cache`: Performance cache (ScriptContext, etc.) that can be cleared at finalize

## Usage

### 1. DI Registration

```csharp
// All components are registered automatically
services.AddTransitionPipeline();

// Configure re-entry options if needed
services.Configure<ReentryOptions>(options =>
{
    options.MaxAutoHops = 15;
    options.AllowInlineAuto = true; // Enable inline automatic transition chaining
});
```

### 2. Transition Execution

```csharp
// Create execution context
var executionContext = new WorkflowExecutionContext
{
    Domain = "example",
    InstanceId = instanceId,
    WorkflowKey = "approval-workflow",
    TransitionKey = "approve",
    TriggerType = TriggerType.Manual,
    Mode = ExecMode.Sync, // or ExecMode.Async for background processing
    Actor = ExecutionActor.User,
    CorrelationId = Guid.NewGuid().ToString("N"),
    Data = jsonData,
    Headers = requestHeaders
};

// Execute transition using workflow execution service
var result = await workflowExecutionService.ExecuteTransitionAsync(executionContext, cancellationToken);

// Handle result
if (result.IsSuccess)
{
    var context = result.Value!;
    // Transition executed successfully
    // Access instance state: context.Instance
    // Access client response: context.ClientResponse
}
else
{
    // Handle error
    var error = result.Error;
    // Log or process error: error.Code, error.Message
}
```

### 3. Adding a New Pipeline Step

```csharp
public sealed class CustomValidationStep : ITransitionStep
{
    private readonly IBusinessRuleValidator _validator;
    private readonly ILogger<CustomValidationStep> _logger;
    
    public CustomValidationStep(
        IBusinessRuleValidator validator,
        ILogger<CustomValidationStep> logger)
    {
        _validator = validator;
        _logger = logger;
    }
    
    public int Order => 15; // Between CreateTransition (10) and OnExecute (20)
    
    public async Task<Result<StepOutcome>> ExecuteAsync(
        TransitionExecutionContext context, 
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating business rules for transition {TransitionKey}", 
            context.TransitionKey);
        
        // Custom validation logic
        var validationResult = await _validator.ValidateAsync(context, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            // Return error using Result pattern
            return Result<StepOutcome>.Fail(
                Error.Validation("business.rule.failed", validationResult.ErrorMessage));
        }
        
        // Normal continuation
        return Result<StepOutcome>.Ok(StepOutcome.Continue());
        
        // Or conditionally stop the pipeline
        // if (shouldStop)
        //     return Result<StepOutcome>.Ok(StepOutcome.Stop());
        
        // Or skip to a specific step
        // return Result<StepOutcome>.Ok(StepOutcome.SkipTo(LifecycleOrder.Finalize));
    }
}

// Register in DI:
services.AddScoped<ITransitionStep, CustomValidationStep>();
```

### 4. Adding a New Trigger Handler

```csharp
public sealed class WebhookTransitionHandler : TransitionHandlerBase
{
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Event;
    
    protected override async Task PreValidateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Webhook-specific validation
        await ValidateWebhookSignature(context, cancellationToken);
    }
}

// Register in DI:
services.AddScoped<ITransitionHandler, WebhookTransitionHandler>();
```

## Benefits

### 1. Separation of Concerns
- Each pipeline step has a single responsibility
- Trigger handlers only manage their own trigger types
- Execution strategies only handle sync/async differences
- TransitionPipeline focuses only on orchestration and execution order

### 2. Dynamic Flow Control
- Step-level flow control with StepOutcome
- Runtime behavior changes with PipelineDirectives
- Flexible execution scenarios with dynamic re-planning
- Optimized for SubFlow and automatic transition chains
- Support for skip-to semantics and conditional execution

### 3. Exception-Free Error Handling
- Result pattern throughout the pipeline
- No exceptions for flow control
- Explicit error propagation
- Better error tracking and debugging
- Consistent error handling across all steps

### 4. Testability
- Each component can be tested independently
- Easy mocking with dependency injection
- Pipeline execution can be tested with different directive scenarios
- Different flow scenarios can be tested with StepOutcome
- Clear boundaries between components

### 5. Extensibility
- New pipeline steps can be easily added without modifying existing code
- New trigger handlers can be added for custom trigger types
- Custom flow control with StepOutcome factory methods
- Directive-based behavior customization
- Order-based step insertion at any point in lifecycle

### 6. Performance
- Re-entry system optimization with inline execution
- Distributed locking at service level
- Idempotency control with concurrency tokens
- Inline automatic transition chaining (prevents unnecessary I/O and background jobs)
- Dynamic step skipping based on directives (prevents unnecessary operations)
- Efficient plan building with minimal allocations

### 7. Observability
- Structured logging with correlation IDs
- OpenTelemetry distributed tracing
- Detailed telemetry for each step with timing
- Re-planning events tracking
- Step-level spans for detailed trace analysis
- Comprehensive error logging with context

## Key Architecture Changes

### Removed Components
1. **IPipelinePlanner & DefaultPlanner**: Planning logic moved into `TransitionPipeline.BuildExecutionPlan()`
2. **Service Locator Pattern**: Services now injected into steps/handlers, not stored in context
3. **Exception-based Flow Control**: Replaced with Result pattern

### Current Implementation
1. **Integrated Planning**: Pipeline builds execution plan dynamically based on directives
2. **Result Pattern**: All operations return `Result<T>` for exception-free error handling
3. **Simplified Architecture**: Fewer abstractions, clearer responsibilities
4. **Performance Optimized**: Inline planning eliminates overhead of separate planner service

## Migration Guide

### From Old Architecture
1. Update step implementations to return `Result<StepOutcome>` instead of `StepOutcome`
2. Replace exception throwing with `Result.Fail()` calls
3. Use `TransitionPipeline` directly instead of through planner abstraction
4. Inject services into steps/handlers instead of retrieving from context
5. Update tests to verify Result pattern behavior

### Adding New Features
1. Implement `ITransitionStep` with Result pattern
2. Set appropriate `Order` constant from `LifecycleOrder`
3. Register in DI container via `AddTransitionPipeline()`
4. Use `PipelineDirectives` for flow control
5. Return `Result<StepOutcome>` for success/failure

## Best Practices

### Pipeline Steps
- Keep steps focused on single responsibility
- Use dependency injection for services
- Return appropriate `StepOutcome` for flow control
- Use Result pattern for error handling
- Log entry/exit for observability

### Trigger Handlers
- Validate trigger-specific requirements in `PreHandleAsync`
- Perform cleanup in `PostHandleAsync`
- Don't modify instance state directly
- Let pipeline steps handle state changes

### Error Handling
- Use Result pattern for expected errors
- Let exceptions bubble for unexpected errors
- Provide meaningful error codes and messages
- Include context in error details

### Performance
- Cache expensive operations in `context.Cache`
- Use `context.Items` for step-to-step data sharing
- Clear cache in finalize step if needed
- Minimize database round-trips

## Related Documentation

- [Strategy Pattern Implementation](./strategy-pattern-implementation.md) - Execution strategy details
- [OpenTelemetry Logging](./opentelemetry-logging.md) - Distributed tracing and logging
- [Background Jobs](./background-jobs.md) - Dapr Jobs integration
- [Task Executors](./task-executors.md) - Task execution system
