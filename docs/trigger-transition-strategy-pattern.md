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

Factory interface for creating HTTP tasks and resolving instance IDs for trigger transitions.

```csharp
public interface ITriggerTransitionHttpTaskFactory
{
    /// <summary>
    /// Creates an HTTP task for trigger transition execution.
    /// </summary>
    HttpTask CreateHttpTask(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        string path,
        string method);
    
    /// <summary>
    /// Resolves the target instance ID using a priority-based mechanism:
    /// 1. Use TriggerInstanceId if provided
    /// 2. Query instance by TriggerKey if provided
    /// 3. Use current instance ID as default
    /// </summary>
    Task<string> ResolveInstanceIdAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken);
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

Handles the Trigger (Direct) trigger type by executing a transition on a target instance.

**Key Features:**
- Validates that TransitionName is provided (required field)
- Resolves target instance ID using `ITriggerTransitionHttpTaskFactory.ResolveInstanceIdAsync`
- Creates path using `InstanceUrlTemplates.Transition` format: `/{domain}/workflows/{flow}/instances/{instanceId}/transitions/{transitionName}`
- Uses HTTP PATCH method
- Delegates to HttpTaskExecutor for execution

**Instance Resolution:**
```csharp
// Resolve instance ID using the factory's ResolveInstanceIdAsync method
var instanceId = await _httpTaskFactory.ResolveInstanceIdAsync(task, context, cancellationToken);
```

This allows the strategy to target:
- A specific instance (if TriggerInstanceId is set)
- An instance by key (if TriggerKey is set)
- The current instance (default behavior)

#### SubProcessTriggerStrategy
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/SubProcessTriggerStrategy.cs`

Placeholder for future implementation of SubProcess trigger type.

- Currently throws `NotImplementedException`
- Reserved for future correlation-based SubFlow triggering

#### GetInstanceDataTriggerStrategy
Location: `src/BBT.Workflow.Application/Tasks/Executors/TriggerTransition/GetInstanceDataTriggerStrategy.cs`

Handles the GetInstanceData trigger type by retrieving instance data from a workflow instance.

**Key Features:**
- **Read Operation**: This is a query operation, not a mutation
- **No Body**: GET requests don't send body data
- **Extensions Support**: Can request specific extensions to enrich the instance data
- **ETag Support**: Supports conditional requests for efficient caching
- Uses HTTP GET method (unlike other strategies that use POST/PATCH)
- Resolves target instance ID using `ITriggerTransitionHttpTaskFactory.ResolveInstanceIdAsync`
- Creates path using `InstanceUrlTemplates.Data` or `InstanceUrlTemplates.DataWithExtensions`
- Delegates to HttpTaskExecutor for execution

**Instance Resolution:**
```csharp
// Resolve instance ID using the factory's ResolveInstanceIdAsync method
var instanceId = await _httpTaskFactory.ResolveInstanceIdAsync(task, context, cancellationToken);
```

This allows the strategy to retrieve data from:
- A specific instance (if TriggerInstanceId is set)
- An instance by key (if TriggerKey is set - used directly without querying)
- The current instance (default behavior)

**Note:** For GetInstanceData type, when TriggerKey is provided, it's used directly as the instance identifier without an additional query.

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

Factory that creates HTTP tasks for trigger transitions and resolves instance IDs.

**Key Responsibilities:**
- Reads configuration from `vNextApi:BaseUrl` and `vNextApi:ApiVersion`
- Builds full URL with API version
- Prepares headers and body from context
- Creates HttpTask with proper configuration
- Resolves instance IDs using a priority-based mechanism

**Instance ID Resolution (ResolveInstanceIdAsync):**

The factory provides a `ResolveInstanceIdAsync` method that determines the target instance ID using a three-tier priority system:

1. **Priority 1: Use TriggerInstanceId if provided**
   ```csharp
   if (!string.IsNullOrWhiteSpace(task.TriggerInstanceId))
       return task.TriggerInstanceId;
   ```

2. **Priority 2: Query instance by TriggerKey if provided**
   ```csharp
   if (!string.IsNullOrWhiteSpace(task.TriggerKey))
   {
       // For GetInstanceData, use the key directly
       if (task.TriggerType == TriggerTransitionType.GetInstanceData)
           return task.TriggerKey;
       
       // For other types, query the instance and extract the ID
       return await ResolveInstanceIdByKeyAsync(task, context, cancellationToken);
   }
   ```

3. **Priority 3: Use current instance ID as default**
   ```csharp
   return context.Instance.Id.ToString();
   ```

**ResolveInstanceIdByKeyAsync Implementation:**

When TriggerKey is provided but TriggerInstanceId is not, the factory:
1. Creates an HTTP GET request to `/{domain}/workflows/{flow}/instances/{key}`
2. Calls the HttpTaskExecutor to retrieve the instance data
3. Extracts the instance ID from the response
4. Restores the original context body
5. Returns the resolved instance ID

This mechanism allows flexible instance targeting without requiring the exact instance ID upfront.

**Instance Resolution Summary by Strategy:**

| Strategy | Uses ResolveInstanceIdAsync | Behavior |
|----------|---------------------------|----------|
| **StartTriggerStrategy** | ❌ No | Creates new instance - no resolution needed |
| **DirectTriggerStrategy** | ✅ Yes | Resolves target instance using priority mechanism |
| **GetInstanceDataTriggerStrategy** | ✅ Yes | Resolves target instance; TriggerKey used directly for GetInstanceData type |
| **SubProcessTriggerStrategy** | 🚧 TBD | Not yet implemented |

**Priority Resolution Flow:**

```
ResolveInstanceIdAsync()
    │
    ├─→ TriggerInstanceId set? ──Yes──→ Use TriggerInstanceId
    │                           └─No
    │
    ├─→ TriggerKey set? ──Yes──→ GetInstanceData type? ──Yes──→ Use TriggerKey directly
    │                    │                              └─No──→ Query instance by key, extract ID
    │                    └─No
    │
    └─→ Use context.Instance.Id (current instance)
```

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

## Script Mapping Examples

The `TriggerTransitionTask` class provides several setter methods that allow dynamic configuration of task properties during script execution (input mapping). This is particularly useful when you need to determine target instances or parameters at runtime.

### Available Setter Methods

#### SetInstance
Sets the target instance ID for the trigger transition.

**Signature:**
```csharp
public void SetInstance(string instanceId)
```

**Example - Using data from current instance:**
```csharp
// Set target instance ID from instance data
triggerTask.SetInstance(context.Instance.Data.getDataInstanceId);
```

**Example - Using response from previous task:**
```csharp
// Set instance ID from a previous HTTP call response
triggerTask.SetInstance(context.Body.data.instanceId);
```

#### SetKey
Sets the target instance key for the trigger transition. The key will be used to resolve the instance ID automatically.

**Signature:**
```csharp
public void SetKey(string key)
```

**Example - Using data from current instance:**
```csharp
// Set target instance key from instance data
triggerTask.SetKey(context.Instance.Data.getDataInstanceKey);
```

**Example - Building a composite key:**
```csharp
// Build a key from multiple data points
var orderKey = $"{context.Instance.Data.customerId}-{context.Instance.Data.orderId}";
triggerTask.SetKey(orderKey);
```

#### SetBody
Sets the body data to send with the trigger transition request.

**Signature:**
```csharp
public void SetBody(dynamic body)
```

**Example - Passing instance data:**
```csharp
// Pass specific data from current instance to target workflow
triggerTask.SetBody(new {
    customerId = context.Instance.Data.customerId,
    orderAmount = context.Instance.Data.totalAmount,
    source = "parent-workflow"
});
```

**Example - Forwarding response from previous task:**
```csharp
// Forward the response from a previous HTTP task
triggerTask.SetBody(context.Body.data);
```

#### SetDomain
Sets the target workflow domain.

**Signature:**
```csharp
public void SetDomain(string domain)
```

**Example:**
```csharp
// Set domain dynamically based on instance data
triggerTask.SetDomain(context.Instance.Data.targetDomain);
```

#### SetFlow
Sets the target workflow name.

**Signature:**
```csharp
public void SetFlow(string flow)
```

**Example:**
```csharp
// Set flow name dynamically
triggerTask.SetFlow(context.Instance.Data.subFlowName);
```

#### SetTriggerType
Sets the trigger type (Start, Trigger, SubProcess, or GetInstanceData).

**Signature:**
```csharp
public void SetTriggerType(string type)
```

**Example:**
```csharp
// Conditionally set trigger type
var triggerType = context.Instance.Data.isNewFlow ? "Start" : "Trigger";
triggerTask.SetTriggerType(triggerType);
```

### Complete Mapping Example

Here's a complete example showing how to configure a trigger transition task dynamically:

```csharp
// Input mapping script for a DirectTriggerStrategy
// Determine target instance from stored correlation data
if (context.Instance.Data.correlatedInstanceId != null)
{
    // Target a specific instance
    triggerTask.SetInstance(context.Instance.Data.correlatedInstanceId);
}
else if (context.Instance.Data.correlatedInstanceKey != null)
{
    // Let the system resolve the instance by key
    triggerTask.SetKey(context.Instance.Data.correlatedInstanceKey);
}

// Set the target domain and flow
triggerTask.SetDomain(context.Instance.Data.targetDomain ?? "default");
triggerTask.SetFlow(context.Instance.Data.targetFlow ?? "main-flow");

// Prepare the body with relevant data
triggerTask.SetBody(new {
    initiatedBy = context.Instance.Key,
    timestamp = DateTime.UtcNow,
    payload = context.Instance.Data.transferData
});
```

### Task Configuration Properties

The `TriggerTransitionTask` class supports the following configuration properties:

| Property | Type | Description | Required | Used By |
|----------|------|-------------|----------|---------|
| `domain` | string | Target workflow domain | ✅ Yes | All strategies |
| `flow` | string | Target workflow name | ✅ Yes | All strategies |
| `type` | enum | Trigger type: Start, Trigger, SubProcess, GetInstanceData | ✅ Yes | All strategies |
| `transitionName` | string | Transition to execute | ⚠️ For Trigger type | DirectTriggerStrategy |
| `key` | string | Target instance key | ❌ Optional | DirectTriggerStrategy, GetInstanceDataTriggerStrategy |
| `instanceId` | string | Target instance ID | ❌ Optional | DirectTriggerStrategy, GetInstanceDataTriggerStrategy |
| `body` | object | Request body data | ❌ Optional | StartTriggerStrategy, DirectTriggerStrategy |
| `version` | string | SubFlow version | ❌ Optional | SubProcessTriggerStrategy |
| `extensions` | string[] | Data extensions to include | ❌ Optional | GetInstanceDataTriggerStrategy |

**Notes:**
- Either `key` or `instanceId` can be provided, with `instanceId` taking priority
- If neither is provided, the current instance ID is used
- The `body` can be set in configuration or dynamically via input mapping
- For `GetInstanceData` type, when `key` is provided, it's used directly without additional querying

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

