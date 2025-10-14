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
    Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}
```

**Note:** Pipeline steps now return `StepOutcome` to provide flow control.

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

1. **CreateTransitionRecordStep** (Order: 10) - Creates the transition record
2. **RunOnExecuteTasksStep** (Order: 20) - Executes the transition's OnExecute tasks
3. **RunOnExitTasksStep** (Order: 30) - Executes the current state's OnExit tasks
4. **ChangeStateStep** (Order: 40) - Performs the state change
5. **RunOnEntryTasksStep** (Order: 50) - Executes the target state's OnEntry tasks
6. **HandleSubFlowOrFinishStep** (Order: 60) - Manages SubFlow or workflow completion
7. **ScheduleTransitionsStep** (Order: 70) - Schedules timed transitions
8. **RunAutomaticTransitionsStep** (Order: 80) - Evaluates and triggers automatic transitions
9. **FinalizeTransitionStep** (Order: 90) - Finalizes the transition and performs cleanup

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

### 5. IPipelinePlanner & DefaultPlanner

The pipeline planner dynamically determines which steps to execute and in what order.

#### IPipelinePlanner
```csharp
public interface IPipelinePlanner
{
    IReadOnlyList<ITransitionStep> Build(
        TransitionExecutionContext context, 
        IEnumerable<ITransitionStep> allSteps);
}
```

#### DefaultPlanner Strategies
- **ResumeFrom**: Start from a specific step (subflow completion, re-planning)
- **Terminal State**: Run only up to Finalize in terminal state
- **Epilogue Mode**: Run or skip Schedule/Auto steps
- **ClearBusy**: BUSY→READY cleanup when starting from Schedule

### 6. PipelineDirectives

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

### 7. StepOutcome

Pipeline step result reporting and flow control:

```csharp
public sealed class StepOutcome
{
    public bool StopPipeline { get; init; }
    public int? SkipToOrder { get; init; }
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
return StepOutcome.Continue();

// Stop the pipeline
return StepOutcome.Stop();

// Skip to a specific step (e.g., return to CreateTransition after inline auto)
return StepOutcome.SkipTo(LifecycleOrder.CreateTransition);

// Mutate directives
return StepOutcome.With(d => d.RequestEpilogue(EpilogueMode.Skip));
```

### 8. Dynamic Re-planning

The pipeline detects directive changes during execution and rebuilds the plan:

**Re-planning Scenarios:**
1. **SubFlow Initiation**: Switch to epilogue SKIP mode
2. **Inline Auto Chain**: Restart from CreateTransition
3. **SubFlow Completion**: Resume from Schedule step
4. **Terminal State**: Execute only up to Finalize

**Pipeline Re-planning Flow:**
```csharp
// 1) Create initial plan
var plan = _planner.Build(context, _steps);

while (i < plan.Count)
{
    var outcome = await step.ExecuteAsync(context, cancellationToken);
    
    // Mutate directives
    outcome.MutateDirectives?.Invoke(context.Directives);
    
    // Re-plan if SkipTo is set
    if (outcome.SkipToOrder is { } skipTo)
    {
        context.Directives.RequestResumeFrom(skipTo);
        plan = _planner.Build(context, _steps);
        i = 0;
        continue;
    }
    
    // Re-plan if directives changed
    if (NeedsReplan(plan, context.Directives))
    {
        context.Directives.RequestResumeFrom(step.Order + 1);
        plan = _planner.Build(context, _steps);
        i = 0;
        continue;
    }
}
```

### 9. TransitionExecutionContext

Minimal, service-free context structure:

```csharp
public sealed class TransitionExecutionContext
{
    // Identity (immutable)
    public string Domain { get; init; }
    public Guid InstanceId { get; init; }
    public string WorkflowKey { get; init; }
    public string TransitionKey { get; init; }
    public TriggerType Trigger { get; init; }
    
    // Definitions (rehydrated)
    public Definitions.Workflow Workflow { get; init; }
    public WorkflowState Current { get; set; }
    public WorkflowTransition Transition { get; init; }
    
    // Instance snapshot
    public InstanceAggregate Instance { get; set; }
    
    // Execution flags
    public bool SkipImmediateExecution { get; set; }
    public bool IsReentry { get; init; }
    
    // Temporary storage
    public IDictionary<string, object?> Items { get; }
    
    // Pipeline directives (new!)
    public PipelineDirectives Directives { get; init; }
    
    // Helper method
    public ScriptContext GetOrBuildScriptContext(Func<ScriptContext> factory);
}
```

**TransitionExecutionContext Properties:**
- **Directives**: Controls pipeline behavior
- **Items**: Temporary storage for data sharing between steps
- **IsReentry**: Re-entry scenario (Auto/Schedule) check

## Usage

### 1. DI Registration

```csharp
services.AddTransitionPipeline();

// Or with custom configuration:
services.AddTransitionPipeline(options =>
{
    options.MaxAutoHops = 15;
    options.AllowInlineAuto = false;
});
```

### 2. Transition Execution

```csharp
var input = new WorkflowExecutionInput
{
    Domain = "example",
    InstanceId = instanceId,
    WorkflowKey = "my-workflow",
    TransitionKey = "approve",
    TriggerType = TriggerType.Manual,
    Mode = ExecMode.Sync
};

await stateMachineExecutor.ExecuteTransitionAsync(input, cancellationToken);
```

### 3. Adding a New Pipeline Step

```csharp
public sealed class CustomValidationStep : ITransitionStep
{
    public int Order => 15; // Between CreateTransition (10) and OnExecute (20)
    
    public async Task<StepOutcome> ExecuteAsync(
        TransitionExecutionContext context, 
        CancellationToken cancellationToken)
    {
        // Custom validation logic
        await ValidateBusinessRules(context, cancellationToken);
        
        // Normal continuation
        return StepOutcome.Continue();
        
        // Or conditionally stop the pipeline
        // if (validationFailed)
        //     return StepOutcome.Stop();
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
- Pipeline planner only handles step selection and ordering

### 2. Dynamic Flow Control
- Step-level flow control with StepOutcome
- Runtime behavior changes with PipelineDirectives
- Flexible execution scenarios with re-planning
- Optimized for SubFlow and Auto transitions

### 3. Testability
- Each component can be tested independently
- Easy mocking
- Pipeline planner can be tested separately
- Different flow scenarios can be tested with StepOutcome

### 4. Extensibility
- New pipeline steps can be easily added
- New trigger handlers can be added
- Custom planner implementations can be written
- Custom flow control with StepOutcome

### 5. Performance
- Re-entry system optimization
- Distributed locking
- Idempotency control
- Inline auto chain execution (prevents unnecessary I/O)
- Dynamic step skipping (prevents unnecessary operations)

### 6. Observability
- Structured logging
- Metrics collection
- Distributed tracing support
- Detailed telemetry for each step
- Re-planning events tracking

## Migration Plan

1. ✅ Create new architecture components
2. ✅ Implement pipeline steps
3. ✅ Create trigger handlers
4. ✅ Set up re-entry system
5. ✅ Update DI registrations
6. 🔄 Migrate existing code to new architecture (gradual)
7. 🔄 Update E2E tests
8. 🔄 Update monitoring and metrics
9. 🔄 Remove old code

## Notes

- This refactoring attempts to maintain backward compatibility
- Existing APIs continue to work
- New features should be developed on the new architecture
- Performance tests should be conducted and compared
