# Strategy Pattern Implementation for Workflow Execution

## Problem Statement

The original `InstanceCommandAppService` had significant code duplication and behavioral inconsistencies when handling synchronous vs asynchronous workflow execution. The main issues were:

1. **Code Duplication**: The same workflow execution logic was repeated in both sync and async paths
2. **Large Application Service**: Many private methods made the service bloated and hard to maintain
3. **Mixed Responsibilities**: Background job scheduling was mixed with business logic
4. **Difficult Testing**: Tightly coupled dependencies made unit testing challenging

## Solution Architecture

### Strategy Pattern Implementation

We implemented the Strategy Pattern to separate sync and async execution strategies:

```
ITransitionStrategy
├── SyncTransitionStrategy  (Immediate execution with pipeline)
└── AsyncTransitionStrategy (Background job scheduling)
```

### Architecture Flow

The execution flow follows this pattern:

```
WorkflowExecutionService
    ↓
ExecutionStrategyFactory (selects strategy based on ExecMode)
    ↓
ITransitionStrategy (SyncTransitionStrategy or AsyncTransitionStrategy)
    ↓
TransitionPipeline (only in SyncTransitionStrategy)
    ↓
ITransitionStep[] (ordered pipeline steps)
```

### Key Components

#### 1. Execution Contexts

##### WorkflowExecutionContext
Input context for initiating workflow execution. Contains:
- Domain/tenant identifier
- Instance and workflow identifiers
- Transition key to execute
- Trigger type (Manual, Automatic, Scheduled, Event)
- Execution mode (Sync/Async)
- Execution actor (User/System)
- Correlation and causation identifiers
- Headers, route values, and data payload

##### TransitionExecutionContext
Runtime context for transition execution within the pipeline. Contains:
- Identity information (domain, instance ID, workflow key, transition key)
- Workflow definitions (workflow, current state, target state, transition)
- Instance snapshot (instance aggregate, data, concurrency token)
- Execution flags (skip immediate execution, is re-entry)
- Telemetry data (trace ID, span ID)
- Headers, route values, and temporary storage (Items dictionary)
- Pipeline directives (flow control)

#### 2. Strategy Factory
- `IExecutionStrategyFactory`: Interface for strategy resolution
- `ExecutionStrategyFactory`: Uses DI to resolve and return appropriate strategies based on `ExecMode`
- Throws `NotSupportedException` when no strategy found for execution mode
- Logs strategy resolution for observability

#### 3. Core Service
- `WorkflowExecutionService`: Orchestrates execution using Result pattern for exception-free error handling
- Coordinates between validation, preparation, handlers, and execution strategies
- Manages distributed locking for instance-level concurrency control
- Provides structured logging and distributed tracing

## Benefits

### 1. **Separation of Concerns**
- Sync logic separated from async logic
- Background job scheduling isolated to async strategies
- Validation and preparation centralized in the service

### 2. **Code Reusability**
- Common preparation logic shared between strategies
- Easy to add new execution modes (e.g., scheduled execution)

### 3. **Testability**
- Each strategy can be tested independently
- Clear boundaries between components
- Mockable dependencies

### 4. **Maintainability**
- Simplified `InstanceCommandAppService` (from ~690 lines to ~100 lines)
- Single responsibility for each class
- Easy to modify individual execution strategies

### 5. **Extensibility**
- New execution strategies can be added without modifying existing code
- Factory pattern makes strategy selection configurable

## Usage Example

```csharp
// Registration in DI container
services.AddTransitionPipeline();

// Usage in application service
public async Task<Result<TransitionExecutionContext>> ExecuteTransitionAsync(
    WorkflowExecutionContext context,
    CancellationToken cancellationToken = default)
{
    return await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);
}

// Creating execution context
var executionContext = new WorkflowExecutionContext
{
    Domain = "example",
    InstanceId = instanceId,
    WorkflowKey = "approval-workflow",
    TransitionKey = "approve",
    TriggerType = TriggerType.Manual,
    Mode = ExecMode.Sync, // or ExecMode.Async
    Actor = ExecutionActor.User,
    Data = jsonData,
    Headers = requestHeaders
};

var result = await workflowExecutionService.ExecuteTransitionAsync(executionContext, cancellationToken);
```

## Implementation Details

### SyncTransitionStrategy
The synchronous strategy executes transitions immediately in the current context using the transition pipeline:

1. **Handler Resolution**: Resolves the appropriate `ITransitionHandler` based on trigger type
2. **Context Creation**: Creates `TransitionExecutionContext` via `ITransitionContextFactory`
3. **Structured Logging**: Establishes logging scope with transition metadata
4. **Distributed Tracing**: Creates OpenTelemetry span for observability
5. **Handler Lifecycle**:
   - `PreHandleAsync`: Pre-processing (validation, authorization, condition checking)
   - `Pipeline.RunAsync`: Executes ordered pipeline steps
   - `PostHandleAsync`: Post-processing (cleanup, notifications, metrics)
6. **Result Pattern**: Returns `Result<TransitionExecutionContext>` for exception-free error handling

**Key Features:**
- Direct execution with distributed locking (managed at service level)
- Immediate response with final status
- Full pipeline execution with all lifecycle steps
- Exception-free error handling using Result pattern
- Comprehensive telemetry and structured logging

### AsyncTransitionStrategy
The asynchronous strategy schedules transitions as background jobs for better scalability:

1. **Context Creation**: Creates execution context for state reservation
2. **Job Payload Preparation**: Prepares `TransitionJobPayload` with all necessary data
3. **Job Scheduling**: Enqueues background job via `IBackgroundJobService` (Dapr Jobs)
4. **Immediate Return**: Returns immediately without waiting for execution

**Key Features:**
- Background job scheduling using Dapr Jobs
- Immediate response (fire-and-forget)
- Better scalability and fault tolerance
- Job metadata for tracking and monitoring
- Result pattern for enqueue operation

### Error Handling
- **Result Pattern**: All operations return `Result<T>` instead of throwing exceptions
- **Distributed Lock**: Managed at `WorkflowExecutionService` level for all strategies
- **Pipeline Errors**: Pipeline steps return `Result<StepOutcome>` to indicate success or failure
- **Background Job Failures**: Handled by Dapr Jobs retry mechanism
- **Error Propagation**: Errors are converted to appropriate exceptions by middleware

## Testing Strategy

- Unit tests for each strategy independently
- Integration tests for the complete workflow
- Mock-based testing for external dependencies
- Behavior verification for state changes

## DI Registration

The strategy pattern components are registered using the extension method:

```csharp
// In WorkflowApplicationModuleServiceCollectionExtensions.cs
public static IServiceCollection AddTransitionPipeline(this IServiceCollection services)
{
    // Context Factory
    services.AddScoped<ITransitionContextFactory, TransitionContextFactory>();
    services.AddScoped<IContextRefresher, ContextRefresher>();

    // Validation Services
    services.AddScoped<ITransitionValidationService, TransitionValidationService>();

    // Trigger Handlers
    services.AddScoped<ITransitionHandler, ManualTransitionHandler>();
    services.AddScoped<ITransitionHandler, AutomaticTransitionHandler>();
    services.AddScoped<ITransitionHandler, ScheduledTransitionHandler>();
    services.AddScoped<ITransitionHandler, EventTransitionHandler>();
    services.AddScoped<ITransitionHandlerFactory, TransitionHandlerFactory>();

    // Execution Strategies
    services.AddScoped<SyncTransitionStrategy>();
    services.AddScoped<AsyncTransitionStrategy>();
    services.AddScoped<IExecutionStrategyFactory, ExecutionStrategyFactory>();

    // Pipeline Steps (registered in execution order)
    services.AddScoped<ITransitionStep, ForwardToActiveSubflowStep>();
    services.AddScoped<ITransitionStep, CreateTransitionRecordStep>();
    services.AddScoped<ITransitionStep, RunOnExecuteTasksStep>();
    services.AddScoped<ITransitionStep, RunOnExitTasksStep>();
    services.AddScoped<ITransitionStep, ChangeStateStep>();
    services.AddScoped<ITransitionStep, RunOnEntryTasksStep>();
    services.AddScoped<ITransitionStep, HandleSubFlowStep>();
    services.AddScoped<ITransitionStep, ClearBusyOnResumeStep>();
    services.AddScoped<ITransitionStep, ScheduleTransitionsStep>();
    services.AddScoped<ITransitionStep, RunAutomaticTransitionsStep>();
    services.AddScoped<ITransitionStep, HandleFinishStep>();
    services.AddScoped<ITransitionStep, FinalizeTransitionStep>();
    services.AddScoped<ITransitionStep, ProcessInlineAutoChainStep>();

    // Pipeline
    services.AddScoped<TransitionPipeline>();

    // Re-entry System
    services.AddScoped<IReentryDispatcher, DefaultReentryDispatcher>();
    
    return services;
}
```

## Migration Guide

### For Existing Code
1. Use `WorkflowExecutionContext` instead of separate input DTOs
2. Work with `Result<T>` pattern instead of exceptions for flow control
3. Update DI registrations to use `AddTransitionPipeline()`
4. Handle `Result<TransitionExecutionContext>` returns instead of void/Task

### For New Features
1. Implement `ITransitionStrategy` for new execution modes
2. Register strategy in `ExecutionStrategyFactory`
3. Add corresponding `ExecMode` enum value
4. Strategy will be automatically selected based on execution mode

## Performance Considerations

- **Memory**: Reduced memory footprint due to smaller service classes and scoped dependencies
- **CPU**: No significant impact, same business logic with optimized pipeline execution
- **I/O**: Background job scheduling remains unchanged, optimized distributed locking
- **Scalability**: Better horizontal scaling due to cleaner separation and stateless design
- **Latency**: 
  - Sync mode: Slightly lower due to streamlined pipeline
  - Async mode: Immediate response with background processing

## Integration with Other Patterns

### Result Pattern
All strategies and pipeline steps use the Result pattern for exception-free error handling:
```csharp
Task<Result<TransitionExecutionContext>> ExecuteAsync(...)
Task<Result<StepOutcome>> ExecuteAsync(...)
```

### Handler Pattern
Trigger-specific pre/post processing via `ITransitionHandler`:
- `ManualTransitionHandler`: User-initiated transitions
- `AutomaticTransitionHandler`: Condition-based transitions
- `ScheduledTransitionHandler`: Timer-based transitions
- `EventTransitionHandler`: Event-driven transitions

### Pipeline Pattern
See [Transition Pipeline Architecture](./transition-pipeline-architecture.md) for detailed pipeline documentation.

## Related Patterns

- **Strategy Pattern**: Core pattern for execution mode selection
- **Factory Pattern**: Used in `ExecutionStrategyFactory` and `TransitionHandlerFactory`
- **Pipeline Pattern**: Used in `TransitionPipeline` for ordered step execution
- **Result Pattern**: Used throughout for exception-free error handling
- **Dependency Injection**: Used throughout for loose coupling and testability
- **Chain of Responsibility**: Used in handler selection and pipeline execution
