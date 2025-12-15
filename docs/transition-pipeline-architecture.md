# Transition Pipeline Architecture

This documentation describes the **Transition Pipeline Architecture** implemented in the BBT.Workflow project.

## Overview

The architecture is designed to make transition execution **readable, testable, and extensible**. It reduces complexity on the `StateMachineExecutor` and defines Auto/Schedule **re-entry** scenarios as first-class citizens.

## Core Principles

- **SRP & Separation of Concerns:** Sync/Async mode selection ≠ Trigger type management ≠ Lifecycle step execution
- **Deterministic Lifecycle:** Well-defined and documented sequence
- **Context Rehydration:** Context is not carried over in Auto/Schedule re-entry; it is rebuilt in a new DI scope
- **No Service Locator:** Services are not in Context but injected into steps/handlers via DI
- **Idempotency & Lock:** Instance-based locking and idempotency is a core feature
- **Post-Commit Processing:** Inline auto chain runs after UoW commit for data consistency

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

**Aether SDK Integration:** All pipeline steps use `[Trace]` attribute for automatic OpenTelemetry span creation:

```csharp
public sealed class ChangeStateStep : ITransitionStep
{
    public int Order => LifecycleOrder.ChangeState;

    [Trace]  // Aether SDK aspect for distributed tracing
    public async Task<Result<StepOutcome>> ExecuteAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Set display name for trace visualization
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ChangeStateStep)}");
        
        // Railway chain with Tap and OnSuccess for side effects
        return await Result.Ok(BuildStateTransitionInfo(context))
            .Tap(info => RecordTransitionMetric(context, info))
            .TapAsync(info => PerformStateChangeAsync(context, info, cancellationToken))
            .ThenAsync(_ => UpdateTargetStateInContext(context))
            .OnSuccess(_ => RecordStateEntryMetric(context))
            .OnSuccess(_ => LogStateChange(context))
            .MapAsync(_ => StepOutcome.Continue());
    }
}
```

#### ITransitionHandler
```csharp
public interface ITransitionHandler
{
    bool CanHandle(TriggerType triggerType);
    Task PreHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
    Task PostHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}
```

### 2. TransitionRunner (New)

The `TransitionRunner` orchestrates transition chaining with isolated DI scope and UoW per hop. This is the entry point for transition execution.

#### ITransitionRunner Interface
```csharp
public interface ITransitionRunner
{
    /// <summary>
    /// Runs a transition and any subsequent inline auto chain transitions.
    /// Each hop is executed in a new DI scope with RequiresNew UoW for complete isolation.
    /// </summary>
    Task<Result<TransitionOutput>> RunAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}
```

#### TransitionRunner Implementation
```csharp
public sealed class TransitionRunner(
    IServiceScopeFactory scopeFactory,
    IOptions<ReentryOptions> options,
    ILogger<TransitionRunner> logger) : ITransitionRunner
{
    private readonly ReentryOptions _options = options.Value;

    [Trace]
    public async Task<Result<TransitionOutput>> RunAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var queue = new Queue<WorkflowExecutionContext>();
        queue.Enqueue(context);

        var hop = 0;
        TransitionOutput? lastOutput = null;

        while (queue.TryDequeue(out var current))
        {
            hop++;

            // Guard: prevent infinite loops
            if (hop > _options.MaxAutoHops)
            {
                return Result<TransitionOutput>.Fail(
                    Error.Validation("auto.chain.maxhops", $"Auto chain exceeded max hops: {_options.MaxAutoHops}"));
            }

            // Execute in isolated scope + UoW
            var hopResult = await ExecuteHopAsync(current, cancellationToken);
            if (!hopResult.IsSuccess)
                return Result<TransitionOutput>.Fail(hopResult.Error);

            var coreOutput = hopResult.Value!;
            lastOutput = coreOutput.Output;

            // POST-COMMIT: enqueue inline auto chain transitions
            if (coreOutput.DirectivesSnapshot.HasQueuedTransitions)
            {
                foreach (var cmd in coreOutput.DirectivesSnapshot.InlineAutoQueue)
                {
                    var nextCmd = cmd with { ChainDepth = cmd.ChainDepth + 1 };
                    
                    if (nextCmd.ChainDepth <= _options.MaxAutoHops)
                    {
                        queue.Enqueue(WorkflowExecutionContext.From(nextCmd));
                    }
                }
            }
        }

        return lastOutput is null
            ? Result<TransitionOutput>.Fail(Error.Failure("transition.output.missing", "Transition output missing"))
            : Result<TransitionOutput>.Ok(lastOutput);
    }

    /// <summary>
    /// Executes a single transition hop in a new DI scope with RequiresNew UoW.
    /// </summary>
    private async Task<Result<TransitionCoreOutput>> ExecuteHopAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var core = sp.GetRequiredService<IWorkflowExecutionCore>();

        await using var uow = await uowManager.BeginAsync(
            new UnitOfWorkOptions { Scope = UnitOfWorkScopeOption.RequiresNew },
            cancellationToken);

        var coreResult = await core.ExecuteTransitionCoreAsync(context, cancellationToken);
        if (!coreResult.IsSuccess)
            return Result<TransitionCoreOutput>.Fail(coreResult.Error);

        // Commit is THE boundary - post-commit processing happens after this
        await uow.CommitAsync(cancellationToken);

        return coreResult;
    }
}
```

**Key Design Decisions:**
- **Isolated DI Scope per Hop**: Each transition runs in a new DI scope
- **RequiresNew UoW**: Complete isolation from any ambient UoW
- **Post-Commit Processing**: Inline auto chain is processed AFTER UoW commit
- **DirectivesSnapshot**: Captures queue state for cross-UoW boundary transfer
- **MaxAutoHops Guard**: Prevents infinite loops with configurable limit

### 3. IWorkflowExecutionCore

Core transition execution contract without UoW management:

```csharp
public interface IWorkflowExecutionCore
{
    /// <summary>
    /// Executes a single transition's core logic without managing UoW.
    /// Returns both the transition output and directives snapshot for post-commit processing.
    /// </summary>
    Task<Result<TransitionCoreOutput>> ExecuteTransitionCoreAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}
```

#### TransitionCoreOutput
```csharp
/// <summary>
/// Output from core transition execution containing both the transition output
/// and the directives snapshot for post-commit inline auto chain processing.
/// </summary>
public sealed record TransitionCoreOutput(TransitionOutput Output, DirectivesSnapshot DirectivesSnapshot);
```

### 4. Pipeline Steps (Lifecycle Order)

All steps implement `ITransitionStep` and return `Result<StepOutcome>`:

| Order | Step | Description |
|-------|------|-------------|
| 5 | ForwardToActiveSubflowStep | Forwards transition to active subflow if exists (Preflight) |
| 10 | HandleCancelPreflightStep | Handles cancel preflight operations |
| 20 | CreateTransitionRecordStep | Creates the transition record |
| 30 | RunOnExecuteTasksStep | Executes the transition's OnExecute tasks |
| 40 | RunOnExitTasksStep | Executes the current state's OnExit tasks |
| 50 | ChangeStateStep | Performs the state change |
| 60 | RunOnEntryTasksStep | Executes the target state's OnEntry tasks |
| 70 | HandleSubFlowStep | Manages SubFlow operations |
| 79 | ClearBusyOnResumeStep | Clears BUSY status on resume |
| 80 | ScheduleTransitionsStep | Schedules timed transitions |
| 90 | RunAutomaticTransitionsStep | Evaluates and enqueues automatic transitions |
| 100 | HandleFinishStep | Handles workflow completion |
| 110 | FinalizeTransitionStep | Finalizes the transition and performs cleanup |
| 111 | AfterEpilogueRefresh | Post-epilogue operations |

### 5. LifecycleOrder

Constants defining the standard execution order for transition lifecycle steps:

```csharp
public static class LifecycleOrder
{
    public const int Preflight = 5;               // Subflow preflight check
    public const int ForwardToActiveSubflow = 10; // Forward to active subflow
    public const int CreateTransition = 20;       // Create transition record
    public const int OnExecute = 30;              // Execute transition's OnExecute tasks
    public const int OnExit = 40;                 // Execute current state's OnExit tasks
    public const int ChangeState = 50;            // Change the instance state
    public const int OnEntry = 60;                // Execute target state's OnEntry tasks
    public const int SubFlow = 70;                // Handle SubFlow operations
    public const int ClearBusyOnResumeStep = Schedule - 1; // Clear BUSY status (79)
    public const int Schedule = 80;               // Schedule future transitions
    public const int Auto = 90;                   // Evaluate automatic transitions
    public const int Finish = 100;                // Handle workflow finishing
    public const int Finalize = 110;              // Finalize transition and cleanup
    public const int AfterEpilogueRefresh = Finalize + 1; // Post-epilogue (111)
}
```

### 6. Trigger Handlers

#### ManualTransitionHandler
- Policy/HMAC/Auth/Schema validation
- User authorization
- Audit logging

#### AutomaticTransitionHandler
- Chain depth control
- Execution metrics
- System state validation

```csharp
public sealed class AutomaticTransitionHandler(
    ITransitionValidationService validationService,
    ILogger<AutomaticTransitionHandler> logger) : TransitionHandlerBase(logger, validationService)
{
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Automatic;

    protected override async Task PreValidateAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        await ValidateSystemStateAsync(context);
        await base.PreValidateAsync(context, cancellationToken);
    }
}
```

#### ScheduledTransitionHandler
- Timing validation
- Schedule constraints
- Recurring schedule management

#### EventTransitionHandler
- Event source validation
- Event correlation
- Payload validation

### 7. Re-entry System

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

### 8. TransitionPipeline

The pipeline orchestrates the execution of transition lifecycle steps in a deterministic order.

#### Core Responsibilities
- Orders all registered pipeline steps by their `Order` property
- Executes steps sequentially with proper error handling
- Manages dynamic re-planning based on directive changes
- Provides structured logging and distributed tracing for each step
- Returns `Result` to indicate success or failure without throwing exceptions

#### Implementation

```csharp
public class TransitionPipeline
{
    private readonly IReadOnlyList<ITransitionStep> _steps;

    public TransitionPipeline(IEnumerable<ITransitionStep> steps)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
    }

    [Trace]
    public async Task<Result> RunAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        var state = CreateInitialState(context);

        while (state.HasMoreSteps())
        {
            // Guard: Skip immediate execution requested
            if (context.SkipImmediateExecution)
                return Result.Ok();

            // Execute current step
            var stepResult = await ExecuteStepAsync(state.CurrentStep, context, cancellationToken);
            if (!stepResult.IsSuccess)
                return Result.Fail(stepResult.Error);

            // Determine flow control based on step outcome
            var flowControl = DetermineFlowControl(stepResult.Value!, state.CurrentStep, context, state);

            // Apply flow control decision
            if (flowControl.ShouldStop)
                break;

            if (flowControl.ShouldReplan)
            {
                state = CreateInitialState(context);
                continue;
            }

            state = state.MoveNext();
        }

        return Result.Ok();
    }
}
```

#### Execution Plan Building
```csharp
private IReadOnlyList<ITransitionStep> BuildExecutionPlan(TransitionExecutionContext context)
{
    var ordered = _steps.ToList();

    // 1) ResumeFrom start
    var startOrder = context.Directives.ConsumeResumeFrom();
    if (startOrder.HasValue)
        ordered = ordered.Where(s => s.Order >= startOrder.Value).ToList();

    // 2) Subflow terminal short circuit (until Finalize)
    if (context.Directives.TerminalReached)
    {
        var maxOrder = LifecycleOrder.Finalize;
        ordered = ordered.Where(s => s.Order <= maxOrder).ToList();
    }

    // 3) Epilogue policy
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

#### Pipeline State Management
```csharp
/// <summary>
/// Represents the current execution state of the pipeline.
/// Immutable record struct for functional state management.
/// </summary>
private readonly record struct PipelineState(IReadOnlyList<ITransitionStep> Plan, int Index)
{
    public ITransitionStep CurrentStep => Plan[Index];
    public bool HasMoreSteps() => Index < Plan.Count;
    public PipelineState MoveNext() => this with { Index = Index + 1 };
}

/// <summary>
/// Represents flow control decision after step execution.
/// Factory methods provide clear intent.
/// </summary>
private readonly record struct FlowControl(bool ShouldStop, bool ShouldReplan)
{
    public static FlowControl Stop() => new(ShouldStop: true, ShouldReplan: false);
    public static FlowControl Replan() => new(ShouldStop: false, ShouldReplan: true);
    public static FlowControl Continue() => new(ShouldStop: false, ShouldReplan: false);
}
```

### 9. PipelineDirectives

Directive system controlling pipeline behavior:

```csharp
public sealed class PipelineDirectives
{
    /// <summary>
    /// Gets the order number from which to resume pipeline execution.
    /// </summary>
    public int? ResumeFromOrder { get; private set; }
    
    /// <summary>
    /// Gets the epilogue execution mode.
    /// </summary>
    public EpilogueMode Epilogue { get; private set; } = EpilogueMode.Run;
    
    /// <summary>
    /// Gets the queue of re-entry commands for inline automatic transition chain execution.
    /// </summary>
    public Queue<ReentryCommand> InlineAutoQueue { get; } = new();
    
    /// <summary>
    /// Gets a value indicating whether the pipeline has reached a terminal state.
    /// </summary>
    public bool TerminalReached { get; private set; }
    
    /// <summary>
    /// Gets a value indicating whether this execution is resuming from a subflow.
    /// </summary>
    public bool IsSubFlowResume { get; private set; }
    
    // Methods
    public void RequestResumeFrom(int order);
    public int? ConsumeResumeFrom();
    public void RequestEpilogue(EpilogueMode mode);
    public void MarkTerminal();
    public void EnqueueInlineAuto(ReentryCommand command);
    public void MarkAsSubFlowResume();
    public DirectivesSnapshot CreateSnapshot();
}
```

#### DirectivesSnapshot

Immutable snapshot for post-commit processing:

```csharp
/// <summary>
/// Immutable snapshot of pipeline directives for post-commit processing.
/// Used to transfer inline auto queue state across UoW boundaries.
/// </summary>
public sealed record DirectivesSnapshot(ReentryCommand[] InlineAutoQueue)
{
    /// <summary>
    /// Gets a value indicating whether there are any queued inline auto transitions.
    /// </summary>
    public bool HasQueuedTransitions => InlineAutoQueue.Length > 0;
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

### 10. StepOutcome

Pipeline step result reporting and flow control:

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

// Skip to a specific step
return Result<StepOutcome>.Ok(StepOutcome.SkipTo(LifecycleOrder.CreateTransition));

// Mutate directives
return Result<StepOutcome>.Ok(StepOutcome.With(d => d.RequestEpilogue(EpilogueMode.Skip)));

// Error case
return Result<StepOutcome>.Fail(Error.Validation("step.failed", "Validation failed"));
```

### 11. TransitionExecutionContext

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
    public Task<ScriptContext> GetOrBuildScriptContextAsync(
        Func<CancellationToken, Task<ScriptContext>> factory,
        CancellationToken cancellationToken);
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

## Execution Flow

### Complete Transition Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         WorkflowExecutionService                            │
│                    ExecuteTransitionAsync(context)                          │
└─────────────────────────────┬───────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           TransitionRunner                                   │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ while (queue.TryDequeue)                                              │  │
│  │   1. Guard: MaxAutoHops check                                         │  │
│  │   2. ExecuteHopAsync (isolated DI scope + RequiresNew UoW)            │  │
│  │   3. UoW Commit                                                       │  │
│  │   4. POST-COMMIT: Enqueue inline auto transitions from snapshot       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      IWorkflowExecutionCore                                  │
│                  ExecuteTransitionCoreAsync(context)                         │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ 1. GetExecutionStrategy(mode)                                         │  │
│  │ 2. ExecuteStrategyAsync(strategy, context)                            │  │
│  │ 3. BuildCoreOutputAsync (creates DirectivesSnapshot)                  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────┬───────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       TransitionPipeline                                     │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │ Pipeline Steps (Order):                                               │  │
│  │   5  → ForwardToActiveSubflowStep                                     │  │
│  │   10 → HandleCancelPreflightStep                                      │  │
│  │   20 → CreateTransitionRecordStep                                     │  │
│  │   30 → RunOnExecuteTasksStep                                          │  │
│  │   40 → RunOnExitTasksStep                                             │  │
│  │   50 → ChangeStateStep                                                │  │
│  │   60 → RunOnEntryTasksStep                                            │  │
│  │   70 → HandleSubFlowStep                                              │  │
│  │   79 → ClearBusyOnResumeStep                                          │  │
│  │   80 → ScheduleTransitionsStep                                        │  │
│  │   90 → RunAutomaticTransitionsStep (enqueues to InlineAutoQueue)      │  │
│  │  100 → HandleFinishStep                                               │  │
│  │  110 → FinalizeTransitionStep                                         │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Inline Auto Chain Flow

```
Transition A completes
       │
       ▼
RunAutomaticTransitionsStep
       │
       ├── Evaluate all auto transitions
       │
       └── Winner found? 
            │
            ├── Yes → EnqueueInlineAuto(ReentryCommand)
            │          │
            └── No  → Continue (soft-fail)
                       │
                       ▼
                 FinalizeTransitionStep
                       │
                       ▼
              DirectivesSnapshot.CreateSnapshot()
                       │
                       ▼
                  UoW.CommitAsync()
                       │
                       ▼
          ┌────────────────────────────────┐
          │ POST-COMMIT (TransitionRunner) │
          │ DirectivesSnapshot.HasQueued?  │
          │    │                           │
          │    ├── Yes → queue.Enqueue(B)  │
          │    │          │                │
          │    └── No  → return lastOutput │
          └────────────────────────────────┘
                       │
                       ▼
              Next loop iteration
                       │
                       ▼
              Execute Transition B
                  (new scope + UoW)
```

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
    var output = result.Value!;
    // Transition executed successfully
    // Access instance ID: output.Id
    // Access status: output.Status
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
    
    public int Order => 25; // Between CreateTransition (20) and OnExecute (30)
    
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(
        TransitionExecutionContext context, 
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(CustomValidationStep)}");
        
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
- TransitionRunner manages UoW lifecycle and inline chain processing

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

### 4. Post-Commit Safety
- Inline auto chain runs AFTER UoW commit
- DirectivesSnapshot captures queue state
- Cross-UoW boundary state transfer
- Data consistency guaranteed

### 5. Testability
- Each component can be tested independently
- Easy mocking with dependency injection
- Pipeline execution can be tested with different directive scenarios
- Different flow scenarios can be tested with StepOutcome
- Clear boundaries between components

### 6. Extensibility
- New pipeline steps can be easily added without modifying existing code
- New trigger handlers can be added for custom trigger types
- Custom flow control with StepOutcome factory methods
- Directive-based behavior customization
- Order-based step insertion at any point in lifecycle

### 7. Performance
- Re-entry system optimization with inline execution
- Distributed locking at service level
- Idempotency control with concurrency tokens
- Inline automatic transition chaining (prevents unnecessary I/O and background jobs)
- Dynamic step skipping based on directives (prevents unnecessary operations)
- Efficient plan building with minimal allocations
- Isolated DI scope prevents memory leaks

### 8. Observability
- Structured logging with correlation IDs
- OpenTelemetry distributed tracing with `[Trace]` aspect
- Detailed telemetry for each step with timing
- Re-planning events tracking
- Step-level spans for detailed trace analysis
- Comprehensive error logging with context

## Best Practices

### Pipeline Steps
- Keep steps focused on single responsibility
- Use dependency injection for services
- Return appropriate `StepOutcome` for flow control
- Use Result pattern for error handling
- Use `[Trace]` attribute for automatic span creation
- Set `Activity.Current?.SetDisplayName()` for trace visualization
- Use Railway extensions (`Tap`, `OnSuccess`, `BindAsync`) for clean chains
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
- Use `GetOrBuildScriptContextAsync` for ScriptContext caching

## Related Documentation

- [Auto Transition Evaluation](./auto-transition-evaluation.md) - Automatic transition condition evaluation
- [Aether SDK Aspects](./aether-sdk-aspects.md) - Cross-cutting concerns with `[Trace]`, `[UnitOfWork]`, `[Log]`
- [Result Pattern & Railway Programming](./result-pattern-railway.md) - Error handling with Result pattern
- [Strategy Pattern Implementation](./strategy-pattern-implementation.md) - Execution strategy details
- [OpenTelemetry Logging](./opentelemetry-logging.md) - Distributed tracing and logging
- [Background Jobs](./background-jobs.md) - Dapr Jobs integration
- [Task Executors](./task-executors.md) - Task execution system
