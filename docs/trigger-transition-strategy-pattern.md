# Trigger Transition Strategy Pattern Implementation

## Overview

This document describes the refactoring of `TriggerTransitionTaskExecutor` to use the Strategy Pattern, improving code maintainability, extensibility, and adherence to SOLID principles.

## Problem Statement

The original `TriggerTransitionTaskExecutor` had the following issues:

1. **Switch Statement**: Used a switch statement to handle different trigger types, making it difficult to extend
2. **Large Executor Class**: Multiple private methods handling different trigger types made the class bloated
3. **Violation of Open/Closed Principle**: Adding new trigger types required modifying the executor class
4. **Mixed Responsibilities**: HTTP task creation logic was embedded in the executor

## Solution Architecture

### Strategy Pattern Implementation

We implemented the Strategy Pattern to separate different trigger transition behaviors:

```
ITriggerTransitionStrategy (Domain)
├── StartTriggerStrategy (Application) - Creates new workflow instances
├── DirectTriggerStrategy (Application) - Triggers transition on current instance
├── SubProcessTriggerStrategy (Application) - Triggers transition on correlated SubFlow (placeholder)
└── GetInstanceDataTriggerStrategy (Application) - Retrieves instance data from workflow instances
```

### Factory Pattern

A factory is used to resolve the appropriate strategy based on the trigger type:

```
ITriggerTransitionStrategyFactory (Domain)
└── TriggerTransitionStrategyFactory (Application)
```

### HTTP Task Factory

HTTP task creation logic was extracted into a dedicated factory:

```
ITriggerTransitionHttpTaskFactory (Domain)
└── TriggerTransitionHttpTaskFactory (Application)
```

## Architecture Flow

The execution flow follows this pattern:

```
TriggerTransitionTaskExecutor
    ↓
ITriggerTransitionStrategyFactory (resolves strategy based on TriggerTransitionType enum)
    ↓
ITriggerTransitionStrategy (StartTriggerStrategy, DirectTriggerStrategy, SubProcessTriggerStrategy, or GetInstanceDataTriggerStrategy)
    ↓
ITriggerTransitionHttpTaskFactory (creates HttpTask)
    ↓
HttpTaskExecutor (executes HTTP request)
```

## Key Components

### 1. Domain Layer Interfaces

#### ITriggerTransitionStrategy
Location: `src/BBT.Workflow.Domain/Execution/TriggerTransition/ITriggerTransitionStrategy.cs`

Defines the contract for trigger transition execution strategies.

```csharp
public interface ITriggerTransitionStrategy
{
    Task ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken);
}
```

#### ITriggerTransitionStrategyFactory
Location: `src/BBT.Workflow.Domain/Execution/TriggerTransition/Factory/ITriggerTransitionStrategyFactory.cs`

Factory interface for creating appropriate strategies based on trigger type.

```csharp
public interface ITriggerTransitionStrategyFactory
{
    ITriggerTransitionStrategy Get(TriggerTransitionType type);
}
```

#### ITriggerTransitionHttpTaskFactory
Location: `src/BBT.Workflow.Domain/Execution/TriggerTransition/Factory/ITriggerTransitionHttpTaskFactory.cs`

Factory interface for creating HTTP tasks used in trigger transitions.

```csharp
public interface ITriggerTransitionHttpTaskFactory
{
    HttpTask CreateHttpTask(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        string path,
        string method);
}
```

### 2. Application Layer Implementations

#### StartTriggerStrategy
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/StartTriggerStrategy.cs`

Handles the Start trigger type by creating a new workflow instance.

- Creates path: `/{domain}/workflows/{workflow}/instances/start`
- Uses HTTP POST method
- Delegates to HttpTaskExecutor for execution

#### DirectTriggerStrategy
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/DirectTriggerStrategy.cs`

Handles the Trigger (Direct) trigger type by executing a transition on the current instance.

- Creates path using `InstanceUrlTemplates.Transition`
- Uses HTTP PATCH method
- Validates that TransitionName is provided
- Delegates to HttpTaskExecutor for execution

#### SubProcessTriggerStrategy
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/SubProcessTriggerStrategy.cs`

Placeholder for future implementation of SubProcess trigger type.

- Currently throws `NotImplementedException`
- Reserved for future correlation-based SubFlow triggering

#### GetInstanceDataTriggerStrategy
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/GetInstanceDataTriggerStrategy.cs`

Handles the GetInstanceData trigger type by retrieving instance data from a workflow instance.

- Creates path using `InstanceUrlTemplates.Data` or `InstanceUrlTemplates.DataWithExtensions`
- Uses HTTP GET method (unlike other strategies that use POST/PATCH)
- Supports optional `extensions` query parameter for data enrichment
- Supports `If-None-Match` header for ETag-based conditional requests
- Delegates to HttpTaskExecutor for execution

**Key Features:**
- **Read Operation**: This is a query operation, not a mutation
- **No Body**: GET requests don't send body data
- **Extensions Support**: Can request specific extensions to enrich the instance data
- **ETag Support**: Supports conditional requests for efficient caching

**Example Configuration:**
```json
{
  "key": "getSubFlowData",
  "type": "TriggerTransition",
  "config": {
    "domain": "sales",
    "flow": "order-processing",
    "type": "GetInstanceData",
    "extensions": ["pricing", "inventory"]
  }
}
```

#### TriggerTransitionStrategyFactory
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/TriggerTransitionStrategyFactory.cs`

Factory implementation that resolves strategies from the DI container.

```csharp
public ITriggerTransitionStrategy Get(TriggerTransitionType type)
{
    ITriggerTransitionStrategy? strategy = type switch
    {
        TriggerTransitionType.Start => _serviceProvider.GetService<StartTriggerStrategy>(),
        TriggerTransitionType.Trigger => _serviceProvider.GetService<DirectTriggerStrategy>(),
        TriggerTransitionType.SubProcess => _serviceProvider.GetService<SubProcessTriggerStrategy>(),
        TriggerTransitionType.GetInstanceData => _serviceProvider.GetService<GetInstanceDataTriggerStrategy>(),
        _ => null
    };
    
    if (strategy == null)
        throw new NotSupportedException($"No trigger transition strategy found for type {type}");
    
    return strategy;
}
```

#### TriggerTransitionHttpTaskFactory
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/TriggerTransitionHttpTaskFactory.cs`

Factory that creates HTTP tasks for trigger transitions.

- Reads configuration from `vNextApi:BaseUrl` and `vNextApi:ApiVersion`
- Builds full URL with API version
- Prepares headers and body from context
- Creates HttpTask with proper configuration

### 3. Refactored Executor

#### TriggerTransitionTaskExecutor
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransitionTaskExecutor.cs`

Simplified executor that delegates to strategies:

**Before:**
- Had switch statement with multiple cases
- Contained private methods: `HandleCreateNewAsync`, `HandleDirectAsync`, `HandleCorrelationAsync`
- Contained `CreateHttpTask` method
- Injected multiple dependencies

**After:**
- Delegates to strategy factory
- No switch statement
- No private handler methods
- Cleaner constructor with fewer dependencies
- Single responsibility: orchestrate script execution and strategy delegation

```csharp
public async Task<object?> ExecuteAsync(...)
{
    var triggerTask = (task as TriggerTransitionTask)!;
    
    // Check runtime domain
    runtimeInfoProvider.Check(context.Runtime.Domain);
    
    // Prepare input
    await PrepareInputAsync(triggerTask, scriptCode, context, cancellationToken);
    
    // Get appropriate strategy and execute
    var strategy = strategyFactory.Get(triggerTask.TriggerType);
    await strategy.ExecuteAsync(triggerTask, context, cancellationToken);
    
    // Process output
    var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
    
    return outputResponse;
}
```

## Dependency Injection Registration

All strategies and factories are registered in the DI container:

Location: `src/BBT.Workflow.Application/Microsoft/Extensions/DependencyInjection/WorkflowApplicationModuleServiceCollectionExtensions.cs`

```csharp
// Trigger Transition Strategies
services.AddScoped<ITriggerTransitionStrategyFactory, TriggerTransitionStrategyFactory>();
services.AddScoped<ITriggerTransitionHttpTaskFactory, TriggerTransitionHttpTaskFactory>();
services.AddScoped<StartTriggerStrategy>();
services.AddScoped<DirectTriggerStrategy>();
services.AddScoped<SubProcessTriggerStrategy>();
services.AddScoped<GetInstanceDataTriggerStrategy>();
```

## Benefits

### 1. Open/Closed Principle
- New trigger types can be added by creating new strategy classes
- No need to modify existing executor or strategy classes

### 2. Single Responsibility Principle
- Each strategy handles one specific trigger type
- HTTP task creation is separated into its own factory
- Executor focuses on orchestration

### 3. Dependency Inversion Principle
- Executor depends on abstractions (interfaces) not concrete implementations
- Strategies are resolved through DI container

### 4. Improved Testability
- Each strategy can be unit tested independently
- Easier to mock dependencies
- Clearer test scenarios

### 5. Better Maintainability
- Clear separation of concerns
- Easier to understand and modify individual strategies
- Reduced code duplication

### 6. Consistency
- Follows existing codebase patterns (similar to `ITransitionStrategy`, `ExecutionStrategyFactory`)
- Uses the same factory pattern approach
- Maintains architectural consistency

## Future Extensions

To add a new trigger type:

1. Create a new strategy class implementing `ITriggerTransitionStrategy`
2. Add the new enum value to `TriggerTransitionType`
3. Update `TriggerTransitionStrategyFactory` to resolve the new strategy
4. Register the new strategy in the DI container

Example:

```csharp
// 1. Create new strategy
public sealed class CustomTriggerStrategy : ITriggerTransitionStrategy
{
    public async Task ExecuteAsync(...)
    {
        // Custom implementation
    }
}

// 2. Add enum value
public enum TriggerTransitionType
{
    Start = 1,
    Trigger = 2,
    SubProcess = 3,
    Custom = 4  // New type
}

// 3. Update factory
TriggerTransitionType.Custom => _serviceProvider.GetService<CustomTriggerStrategy>(),

// 4. Register in DI
services.AddScoped<CustomTriggerStrategy>();
```

## Migration Notes

- No breaking changes to public APIs
- Existing workflow definitions continue to work without modification
- The refactoring is internal to the execution layer
- All existing trigger types (Start, Trigger) maintain the same behavior

## Related Documentation

- [Strategy Pattern Implementation](strategy-pattern-implementation.md) - General strategy pattern usage in the codebase
- [Task Executors](task-executors.md) - Overview of task execution architecture
- [Architecture Overview](architecture-overview.md) - Overall system architecture

