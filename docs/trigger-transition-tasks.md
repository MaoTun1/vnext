# Trigger Transition Tasks

## Overview

This document describes the trigger transition task types that enable workflow instances to interact with other workflow instances. The system provides four dedicated task types, each with its own executor, for different trigger transition scenarios.

## Architecture

The trigger transition functionality is implemented using four separate task types, each with a dedicated executor:

```
Task Types (Domain)
├── StartTask (TaskType.StartTrigger = 12) - Creates new workflow instances
├── DirectTriggerTask (TaskType.DirectTrigger = 13) - Triggers transition on target instance
├── GetInstanceDataTask (TaskType.GetInstanceData = 14) - Retrieves instance data
└── SubProcessTask (TaskType.SubProcess = 15) - Starts subprocess workflows

Executors (Application)
├── StartTaskExecutor - Executes StartTask
├── DirectTriggerTaskExecutor - Executes DirectTriggerTask
├── GetInstanceDataTaskExecutor - Executes GetInstanceDataTask
└── SubProcessTaskExecutor - Executes SubProcessTask
```

### HTTP Task Factory

HTTP task creation logic is shared across executors through a dedicated factory:

```
ITriggerTransitionHttpTaskFactory (Domain)
└── TriggerTransitionHttpTaskFactory (Application)
```

## Architecture Flow

The execution flow follows this pattern:

```
TaskExecutorFactory (resolves executor based on TaskType)
    ↓
Task Executor (StartTaskExecutor, DirectTriggerTaskExecutor, GetInstanceDataTaskExecutor, or SubProcessTaskExecutor)
    ↓
ITriggerTransitionHttpTaskFactory (creates HttpTask when needed)
    ↓
HttpTaskExecutor (executes HTTP request for Start, DirectTrigger, GetInstanceData)
    OR
ISubflowStarter (starts subprocess for SubProcessTask)
```

## Task Types

### 1. StartTask

**TaskType:** `StartTrigger = 12`  
**Location:** `src/BBT.Workflow.Domain/Definitions/Tasks/StartTask.cs`  
**Executor:** `StartTaskExecutor`

Creates a new workflow instance by calling the workflow start endpoint.

**Properties:**
- `TriggerDomain` (string, required) - Target workflow domain
- `TriggerFlow` (string, required) - Target workflow name
- `Body` (JsonElement?, optional) - Request body data

**Execution:**
- Creates path: `/{domain}/workflows/{workflow}/instances/start`
- Uses HTTP POST method
- Delegates to HttpTaskExecutor for execution

**Example Configuration:**
```json
{
  "key": "startOrderWorkflow",
  "type": "12",
  "config": {
    "domain": "sales",
    "flow": "order-processing",
    "body": {
      "customerId": "12345",
      "orderAmount": 999.99
    }
  }
}
```

### 2. DirectTriggerTask

**TaskType:** `DirectTrigger = 13`  
**Location:** `src/BBT.Workflow.Domain/Definitions/Tasks/DirectTriggerTask.cs`  
**Executor:** `DirectTriggerTaskExecutor`

Executes a transition on a target workflow instance.

**Properties:**
- `TriggerDomain` (string, required) - Target workflow domain
- `TriggerFlow` (string, required) - Target workflow name
- `TransitionName` (string, required) - Transition to execute
- `TriggerKey` (string?, optional) - Target instance key
- `TriggerInstanceId` (string?, optional) - Target instance ID
- `Body` (JsonElement?, optional) - Request body data

**Execution:**
- Validates that `TransitionName` is provided (required field)
- Resolves target instance ID using `ITriggerTransitionHttpTaskFactory.ResolveInstanceIdAsync`
- Creates path using `InstanceUrlTemplates.Transition` format: `/{domain}/workflows/{flow}/instances/{instanceId}/transitions/{transitionName}`
- Uses HTTP PATCH method
- Delegates to HttpTaskExecutor for execution

**Instance Resolution:**
The executor resolves the target instance ID using a priority-based mechanism:
1. Use `TriggerInstanceId` if provided
2. Query instance by `TriggerKey` if provided
3. Use current instance ID as default

**Example Configuration:**
```json
{
  "key": "triggerOrderApproval",
  "type": "13",
  "config": {
    "domain": "sales",
    "flow": "order-processing",
    "transitionName": "approve",
    "instanceId": "550e8400-e29b-41d4-a716-446655440000",
    "body": {
      "approvedBy": "manager123",
      "approvalDate": "2024-01-15T10:30:00Z"
    }
  }
}
```

### 3. GetInstanceDataTask

**TaskType:** `GetInstanceData = 14`  
**Location:** `src/BBT.Workflow.Domain/Definitions/Tasks/GetInstanceDataTask.cs`  
**Executor:** `GetInstanceDataTaskExecutor`

Retrieves instance data from a workflow instance. This is a read-only query operation.

**Properties:**
- `TriggerDomain` (string, required) - Target workflow domain
- `TriggerFlow` (string, required) - Target workflow name
- `TriggerKey` (string?, optional) - Target instance key
- `TriggerInstanceId` (string?, optional) - Target instance ID
- `Extensions` (string[]?, optional) - Data extensions to include for enrichment

**Execution:**
- **Read Operation**: This is a query operation, not a mutation
- **No Body**: GET requests don't send body data
- Resolves target instance ID using `ITriggerTransitionHttpTaskFactory.ResolveInstanceIdAsync`
- Creates path using `InstanceUrlTemplates.Data` or `InstanceUrlTemplates.DataWithExtensions`
- Uses HTTP GET method
- Delegates to HttpTaskExecutor for execution

**Instance Resolution:**
- For `GetInstanceData` type, when `TriggerKey` is provided, it's used directly as the instance identifier without an additional query
- Otherwise follows the standard priority mechanism

**Example Configuration:**
```json
{
  "key": "getSubFlowData",
  "type": "14",
  "config": {
    "domain": "sales",
    "flow": "order-processing",
    "key": "order-12345",
    "extensions": ["pricing", "inventory"]
  }
}
```

### 4. SubProcessTask

**TaskType:** `SubProcess = 15`  
**Location:** `src/BBT.Workflow.Domain/Definitions/Tasks/SubProcessTask.cs`  
**Executor:** `SubProcessTaskExecutor`

Starts a subprocess workflow by creating a correlation and starting the subprocess.

**Properties:**
- `TriggerDomain` (string, required) - Target workflow domain
- `TriggerKey` (string, required) - Target workflow key
- `TriggerVersion` (string?, optional) - SubFlow version
- `Body` (JsonElement?, optional) - Request body data

**Execution:**
- Creates correlation for SubProcess using `InstanceCorrelation.Create`
- Creates a SubFlow reference
- Starts the SubProcess using `ISubflowStarter.SubStartAsync`
- Does not use HTTP calls; directly interacts with SubFlow system

**Example Configuration:**
```json
{
  "key": "startSubProcess",
  "type": "15",
  "config": {
    "domain": "sales",
    "key": "payment-processing",
    "version": "1.0",
    "body": {
      "orderId": "12345",
      "amount": 999.99
    }
  }
}
```

## HTTP Task Factory

### ITriggerTransitionHttpTaskFactory

**Location:** `src/BBT.Workflow.Domain/Execution/TriggerTransition/Factory/ITriggerTransitionHttpTaskFactory.cs`

Factory interface for creating HTTP tasks and resolving instance IDs for trigger transitions.

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
       if (task is GetInstanceDataTask)
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

**Priority Resolution Flow:**

```
ResolveInstanceIdAsync()
    │
    ├─→ TriggerInstanceId set? ──Yes──→ Use TriggerInstanceId
    │                           └─No
    │
    ├─→ TriggerKey set? ──Yes──→ GetInstanceDataTask? ──Yes──→ Use TriggerKey directly
    │                    │                              └─No──→ Query instance by key, extract ID
    │                    └─No
    │
    └─→ Use context.Instance.Id (current instance)
```

**Instance Resolution Summary by Task Type:**

| Task Type | Uses ResolveInstanceIdAsync | Behavior |
|-----------|---------------------------|----------|
| **StartTask** | ❌ No | Creates new instance - no resolution needed |
| **DirectTriggerTask** | ✅ Yes | Resolves target instance using priority mechanism |
| **GetInstanceDataTask** | ✅ Yes | Resolves target instance; TriggerKey used directly |
| **SubProcessTask** | ❌ No | Uses SubFlow system directly - no HTTP resolution |

## Script Mapping Examples

Each task type provides setter methods that allow dynamic configuration of task properties during script execution (input mapping). This is particularly useful when you need to determine target instances or parameters at runtime.

### StartTask Setter Methods

#### SetDomain
Sets the target workflow domain.

```csharp
startTask.SetDomain(context.Instance.Data.targetDomain);
```

#### SetFlow
Sets the target workflow name.

```csharp
startTask.SetFlow(context.Instance.Data.subFlowName);
```

#### SetBody
Sets the body data to send with the start request.

```csharp
startTask.SetBody(new {
    customerId = context.Instance.Data.customerId,
    orderAmount = context.Instance.Data.totalAmount,
    source = "parent-workflow"
});
```

### DirectTriggerTask Setter Methods

#### SetInstance
Sets the target instance ID for the trigger transition.

```csharp
// Set target instance ID from instance data
directTriggerTask.SetInstance(context.Instance.Data.getDataInstanceId);

// Or from a previous HTTP call response
directTriggerTask.SetInstance(context.Body.data.instanceId);
```

#### SetKey
Sets the target instance key for the trigger transition. The key will be used to resolve the instance ID automatically.

```csharp
// Set target instance key from instance data
directTriggerTask.SetKey(context.Instance.Data.getDataInstanceKey);

// Or build a composite key
var orderKey = $"{context.Instance.Data.customerId}-{context.Instance.Data.orderId}";
directTriggerTask.SetKey(orderKey);
```

#### SetDomain
Sets the target workflow domain.

```csharp
directTriggerTask.SetDomain(context.Instance.Data.targetDomain);
```

#### SetFlow
Sets the target workflow name.

```csharp
directTriggerTask.SetFlow(context.Instance.Data.targetFlow);
```

#### SetTransitionName
Sets the transition name to execute.

```csharp
directTriggerTask.SetTransitionName(context.Instance.Data.transitionName);
```

#### SetBody
Sets the body data to send with the transition request.

```csharp
directTriggerTask.SetBody(new {
    initiatedBy = context.Instance.Key,
    timestamp = DateTime.UtcNow,
    payload = context.Instance.Data.transferData
});
```

### GetInstanceDataTask Setter Methods

#### SetInstance
Sets the target instance ID.

```csharp
getDataTask.SetInstance(context.Instance.Data.targetInstanceId);
```

#### SetKey
Sets the target instance key (used directly for GetInstanceData).

```csharp
getDataTask.SetKey(context.Instance.Data.targetInstanceKey);
```

#### SetDomain
Sets the target workflow domain.

```csharp
getDataTask.SetDomain(context.Instance.Data.targetDomain);
```

#### SetFlow
Sets the target workflow name.

```csharp
getDataTask.SetFlow(context.Instance.Data.targetFlow);
```

#### SetExtensions
Sets the extensions to request for data enrichment.

```csharp
getDataTask.SetExtensions(new[] { "pricing", "inventory" });
```

### SubProcessTask Setter Methods

#### SetDomain
Sets the target workflow domain.

```csharp
subProcessTask.SetDomain(context.Instance.Data.targetDomain);
```

#### SetKey
Sets the target workflow key.

```csharp
subProcessTask.SetKey(context.Instance.Data.subProcessKey);
```

#### SetVersion
Sets the SubFlow version.

```csharp
subProcessTask.SetVersion(context.Instance.Data.subProcessVersion);
```

#### SetBody
Sets the body data to send with the subprocess request.

```csharp
subProcessTask.SetBody(new {
    orderId = context.Instance.Data.orderId,
    amount = context.Instance.Data.totalAmount
});
```

### Complete Mapping Example

Here's a complete example showing how to configure a DirectTriggerTask dynamically:

```csharp
// Input mapping script for a DirectTriggerTask
// Determine target instance from stored correlation data
if (context.Instance.Data.correlatedInstanceId != null)
{
    // Target a specific instance
    directTriggerTask.SetInstance(context.Instance.Data.correlatedInstanceId);
}
else if (context.Instance.Data.correlatedInstanceKey != null)
{
    // Let the system resolve the instance by key
    directTriggerTask.SetKey(context.Instance.Data.correlatedInstanceKey);
}

// Set the target domain and flow
directTriggerTask.SetDomain(context.Instance.Data.targetDomain ?? "default");
directTriggerTask.SetFlow(context.Instance.Data.targetFlow ?? "main-flow");
directTriggerTask.SetTransitionName("approve");

// Prepare the body with relevant data
directTriggerTask.SetBody(new {
    initiatedBy = context.Instance.Key,
    timestamp = DateTime.UtcNow,
    payload = context.Instance.Data.transferData
});
```

## Task Configuration Properties Summary

### StartTask

| Property | Type | Description | Required |
|----------|------|-------------|----------|
| `domain` | string | Target workflow domain | ✅ Yes |
| `flow` | string | Target workflow name | ✅ Yes |
| `body` | object | Request body data | ❌ Optional |

### DirectTriggerTask

| Property | Type | Description | Required |
|----------|------|-------------|----------|
| `domain` | string | Target workflow domain | ✅ Yes |
| `flow` | string | Target workflow name | ✅ Yes |
| `transitionName` | string | Transition to execute | ✅ Yes |
| `key` | string | Target instance key | ❌ Optional |
| `instanceId` | string | Target instance ID | ❌ Optional |
| `body` | object | Request body data | ❌ Optional |

**Notes:**
- Either `key` or `instanceId` can be provided, with `instanceId` taking priority
- If neither is provided, the current instance ID is used

### GetInstanceDataTask

| Property | Type | Description | Required |
|----------|------|-------------|----------|
| `domain` | string | Target workflow domain | ✅ Yes |
| `flow` | string | Target workflow name | ✅ Yes |
| `key` | string | Target instance key | ❌ Optional |
| `instanceId` | string | Target instance ID | ❌ Optional |
| `extensions` | string[] | Data extensions to include | ❌ Optional |

**Notes:**
- Either `key` or `instanceId` can be provided, with `instanceId` taking priority
- If neither is provided, the current instance ID is used
- When `key` is provided, it's used directly without additional querying

### SubProcessTask

| Property | Type | Description | Required |
|----------|------|-------------|----------|
| `domain` | string | Target workflow domain | ✅ Yes |
| `key` | string | Target workflow key | ✅ Yes |
| `version` | string | SubFlow version | ❌ Optional |
| `body` | object | Request body data | ❌ Optional |

## Dependency Injection Registration

All executors and the HTTP task factory are registered in the DI container:

**Location:** `src/BBT.Workflow.Application/Microsoft/Extensions/DependencyInjection/WorkflowApplicationModuleServiceCollectionExtensions.cs`

```csharp
// Task Executors
services.AddScoped<StartTaskExecutor>();
services.AddScoped<DirectTriggerTaskExecutor>();
services.AddScoped<GetInstanceDataTaskExecutor>();
services.AddScoped<SubProcessTaskExecutor>();

// HTTP Task Factory for trigger transition tasks
services.AddScoped<ITriggerTransitionHttpTaskFactory, TriggerTransitionHttpTaskFactory>();
```

## Benefits

### 1. Simplified Architecture
- Each task type has a dedicated executor
- No strategy pattern layer - direct execution
- Clear separation of concerns

### 2. Type Safety
- Each task type is strongly typed
- Compile-time checking for task properties
- Better IntelliSense support

### 3. Single Responsibility Principle
- Each executor handles one specific task type
- HTTP task creation is separated into its own factory
- Clear, focused responsibilities

### 4. Improved Testability
- Each executor can be unit tested independently
- Easier to mock dependencies
- Clearer test scenarios

### 5. Better Maintainability
- Clear separation of concerns
- Easier to understand and modify individual executors
- Reduced code complexity

### 6. Object Pooling Support
- All task types support object pooling for high-performance scenarios
- Registered in `PoolableTaskRegistry` for efficient memory usage

## Migration from TriggerTransitionTask

If you have existing workflows using the old `TriggerTransitionTask` (type "11"), you'll need to migrate to the new task types:

### Migration Mapping

| Old TriggerTransitionType | New Task Type | Type Discriminator |
|---------------------------|--------------|-------------------|
| `Start` | `StartTask` | 12 |
| `Trigger` | `DirectTriggerTask` | 13 |
| `GetInstanceData` | `GetInstanceDataTask` | 14 |
| `SubProcess` | `SubProcessTask` | 15 |

### Example Migration

**Before (TriggerTransitionTask):**
```json
{
  "key": "triggerWorkflow",
  "type": "11",
  "config": {
    "domain": "sales",
    "flow": "order-processing",
    "type": "Start",
    "body": { "orderId": "12345" }
  }
}
```

**After (StartTask):**
```json
{
  "key": "triggerWorkflow",
  "type": "12",
  "config": {
    "domain": "sales",
    "flow": "order-processing",
    "body": { "orderId": "12345" }
  }
}
```

## Related Documentation

- [Task Executors](task-executors.md) - Overview of task execution architecture
- [Architecture Overview](architecture-overview.md) - Overall system architecture
- [Task Factory Pooling](task-factory-pooling.md) - Object pooling for task instances

