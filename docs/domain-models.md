# Domain Models

## Overview

The domain layer contains the core business logic and entities that represent the workflow system's domain concepts. These models are framework-agnostic and encapsulate the essential business rules and behaviors.

## Core Entities and Aggregates

### 1. Instance (Aggregate Root)

The `Instance` represents a running workflow instance and serves as the main aggregate root.

```csharp
public sealed class Instance : AggregateRoot<Guid>, IHasCreatedAt, IHasModifyTime
{
    public string? Key { get; private set; }
    public string Flow { get; private set; }
    public string? CurrentState { get; private set; }
    public InstanceStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public List<string> Tags { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool IsTransient { get; private set; }
}
```

**Key Properties:**
- `Key`: Unique business identifier for the instance
- `Flow`: Reference to the workflow definition
- `CurrentState`: Current state in the workflow
- `Status`: Execution status (Active, Completed, Failed)
- `DataList`: Collection of instance data versions

**Business Rules:**
- Instance must have a valid flow reference
- Key must be unique within a schema
- Status transitions follow business rules
- Data versions are immutable once created

**Usage Example:**
```csharp
// Create new instance
var instance = Instance.Create(
    GuidGenerator.Create(),
    "customer-onboarding",
    "CUST-12345");

// Add data with versioning
instance.AddDataWithVersion(
    GuidGenerator.Create(),
    new JsonData(customerData),
    "1.0.0");

// Transition to next state
instance.TransitionTo("document-review", userId);
```

### 2. InstanceData (Entity)

Represents versioned data associated with a workflow instance.

```csharp
public sealed class InstanceData : Entity<Guid>, IHasVersion, IHasEtag
{
    public Guid InstanceId { get; private set; }
    public string Version { get; private set; }
    public string ETag { get; private set; }
    public JsonData Data { get; private set; }
    public DateTime EnteredAt { get; private set; }
    public dynamic? Attributes => Data.JsonElement.ToDynamic();
}
```

**Key Features:**
- **Versioning**: Semantic versioning support (1.0.0, 1.1.0, etc.)
- **Immutability**: Data cannot be modified once created
- **ETag Support**: Optimistic concurrency control
- **JSON Storage**: Flexible data structure using JSONB

**Versioning Strategy:**
```csharp
internal InstanceData NewVersion(
    Guid id,
    JsonData jsonData,
    VersionStrategy versionStrategy)
{
    var newVersion = IncrementVersion(Version, versionStrategy);
    var newData = Data.Merge(jsonData);
    return new InstanceData(id, InstanceId, newVersion, newData);
}
```

### 3. InstanceTransition (Entity)

Represents a state transition within a workflow instance.

```csharp
public sealed class InstanceTransition : Entity<Guid>
{
    public Guid InstanceId { get; private set; }
    public string FromState { get; private set; }
    public string ToState { get; private set; }
    public string TransitionKey { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public TransitionStatus Status { get; private set; }
    public string? UserId { get; private set; }
}
```

**Lifecycle Management:**
- Tracks transition execution timeline
- Maintains audit trail of state changes
- Records user context for transitions

### 4. InstanceTask (Entity)

Represents task execution within a workflow transition.

```csharp
public sealed class InstanceTask : Entity<Guid>
{
    public Guid TransitionId { get; private set; }
    public string TaskId { get; private set; }
    public TaskStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public JsonData Request { get; private set; }
    public JsonData Response { get; private set; }
}
```

**Status Management:**
```csharp
public void Completed(JsonData response)
{
    FinishedAt = DateTime.UtcNow;
    Status = TaskStatus.Completed;
    Response = response;
}

public void Faulted(string reason)
{
    FinishedAt = DateTime.UtcNow;
    Status = TaskStatus.Faulted;
    Response = new JsonData(JsonSerializer.Serialize(new { error = reason }));
}
```

## Workflow Definition Models

### 1. Workflow (Aggregate Root)

Defines the structure and behavior of a workflow.

```csharp
public sealed class Workflow : IDomainEntity, IReference, IReferenceSetter, IHasCreatedAt
{
    public string Key { get; private set; }
    public string Domain { get; private set; }
    public string Flow { get; init; }
    public string Version { get; private set; }
    public WorkflowType Type { get; private set; }
    public WorkflowTimeout Timeout { get; private set; }
    
    public IReadOnlyCollection<LanguageLabel> Labels => labels.AsReadOnly();
    public IReadOnlyCollection<IReference> Functions => functions.AsReadOnly();
    public IReadOnlyCollection<State> States => states.AsReadOnly();
    public Transition StartTransition { get; private set; }
}
```

**Components:**
- **States**: Workflow states and their configurations
- **Transitions**: Allowed transitions between states
- **Functions**: Reusable function definitions
- **Labels**: Multi-language labels for UI

### 2. WorkflowTask (Abstract Base)

Base class for all task types in the system.

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DaprHttpEndpointTask), typeDiscriminator: "1")]
[JsonDerivedType(typeof(DaprBindingTask), typeDiscriminator: "2")]
[JsonDerivedType(typeof(HumanTask), typeDiscriminator: "5")]
[JsonDerivedType(typeof(HttpTask), typeDiscriminator: "6")]
[JsonDerivedType(typeof(ScriptTask), typeDiscriminator: "7")]
public abstract class WorkflowTask : IDomainEntity, ITaskReference, IReferenceSetter
{
    public string Key { get; private set; }
    public string Flow { get; init; }
    public string Domain { get; private set; }
    public string Version { get; private set; }
    public string Type { get; protected set; }
    public JsonElement Config { get; private set; }
}
```

**Task Types:**
- **HttpTask**: HTTP API calls
- **DaprServiceTask**: DAPR service invocation
- **DaprHttpEndpointTask**: DAPR HTTP endpoint calls
- **ScriptTask**: Script execution
- **HumanTask**: Human intervention required

### 3. HumanTask (Derived Task)

Specialized task requiring human interaction.

```csharp
public class HumanTask : WorkflowTask
{
    public string Title { get; private set; }
    public string Instructions { get; private set; }
    public string AssignedTo { get; private set; }
    public DateTime? DueDate { get; private set; }
    public JsonElement Form { get; private set; }
    public int ReminderIntervalMinutes { get; private set; }
    public int EscalationTimeoutMinutes { get; private set; }
    public string EscalationAssignee { get; private set; }
}
```

## Value Objects

### 1. JsonData

Encapsulates JSON data with serialization support.

```csharp
public class JsonData
{
    public string Json { get; init; }
    public JsonElement JsonElement => JsonDocument.Parse(Json).RootElement;
    
    public JsonData Merge(JsonData other)
    {
        // Merge two JSON objects
        var merged = JsonMerger.Merge(this.JsonElement, other.JsonElement);
        return new JsonData(merged.ToString());
    }
}
```

### 2. Reference

Represents a reference to a domain entity.

```csharp
public sealed class Reference : IReference
{
    public string Key { get; init; }
    public string Domain { get; init; }
    public string Flow { get; init; }
    public string Version { get; init; }
    
    public Reference ToReference() => this;
}
```

### 3. ScriptCode

Encapsulates script code with validation.

```csharp
public class ScriptCode
{
    public string Code { get; init; }
    public ScriptLanguage Language { get; init; }
    
    public bool IsValid => !string.IsNullOrWhiteSpace(Code);
}
```

## Domain Interfaces

### 1. IDomainEntity

Base interface for all domain entities.

```csharp
public interface IDomainEntity : IHasKey, IHasVersion, IHasDomain
{
    string CacheKey { get; }
}

public interface IHasKey
{
    string Key { get; }
}

public interface IHasDomain
{
    string Domain { get; }
}

public interface IHasVersion
{
    string Version { get; }
}
```

### 2. Repository Interfaces

Domain-defined repository contracts.

```csharp
public interface IInstanceRepository : IRepository<Instance, Guid>
{
    Task<Instance?> FindByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<Instance> GetActiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<InstanceAndDataModel>> GetActiveDataListAsync(CancellationToken cancellationToken = default);
}

public interface IInstanceTaskRepository : IRepository<InstanceTask, Guid>
{
    // Task-specific repository methods
}
```

## Domain Services

### 1. Rule Engine

Processes business rules against contexts.

```csharp
public interface IRuleEngine<T>
{
    void Process(T context);
    void SetRules(IEnumerable<IRule<T>> rules);
}

public interface IRule<T>
{
    bool IsApplicable(T context);
    void Execute(T context);
}

public abstract class BaseRule<T> : IRule<T>
{
    public abstract bool IsApplicable(T context);
    public abstract void Execute(T context);
}
```

**Usage Example:**
```csharp
public class CreditCheckRule : BaseRule<LoanApplicationContext>
{
    public override bool IsApplicable(LoanApplicationContext context)
    {
        return context.LoanAmount > 10000;
    }
    
    public override void Execute(LoanApplicationContext context)
    {
        // Perform credit check logic
        context.RequiresCreditCheck = true;
    }
}
```

## Domain Events

### 1. Event Definitions

```csharp
public class InstanceStartedEvent
{
    public Guid InstanceId { get; set; }
    public string WorkflowKey { get; set; }
    public string Domain { get; set; }
    public DateTime StartedAt { get; set; }
}

public class InstanceCompletedEvent
{
    public Guid InstanceId { get; set; }
    public string WorkflowKey { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
}
```

## Constants and Configurations

### 1. Domain Constants

```csharp
public class WorkflowConstants
{
    public const string DefaultVersion = "1.0.0";
    public const int MaxKeyLength = 100;
    public const int MaxVersionLength = 20;
    public const int MaxDomainLength = 50;
}

public class InstanceConstants
{
    public const int MaxKeyLength = WorkflowConstants.MaxKeyLength;
    public const int MaxStatusLength = 20;
}
```

### 2. Enumerations

```csharp
public enum InstanceStatus
{
    Active = 1,
    Completed = 2,
    Failed = 3,
    Suspended = 4
}

public enum TaskStatus
{
    Waiting = 1,
    Running = 2,
    Completed = 3,
    Faulted = 4
}

public enum WorkflowType
{
    Standard = 1,
    SubFlow = 2
}
```

## Validation Rules

The domain models include built-in validation rules:

```csharp
// Instance validation
private void SetKey(string? key)
{
    Key = Check.Length(key, nameof(Key), InstanceConstants.MaxKeyLength);
}

private void SetFlow(string flow)
{
    Flow = Check.NotNullOrWhiteSpace(flow, nameof(Flow), WorkflowConstants.MaxKeyLength);
}

// Workflow validation
private void SetVersion(string version)
{
    Version = Check.NotNullOrWhiteSpace(version, nameof(Version), WorkflowConstants.MaxVersionLength);
}
```

## Entity Relationships

```
Instance (1) ──────── (*) InstanceData
    │
    ├── (1) ──────── (*) InstanceTransition
    │
    ├── (1) ──────── (*) InstanceTask
    │
    └── (1) ──────── (*) InstanceCorrelation

Workflow (1) ──────── (*) State
    │
    ├── (1) ──────── (*) Function
    │
    ├── (1) ──────── (*) Extension
    │
    └── (1) ──────── (*) Transition
```

This domain model design ensures strong business rule enforcement, clear entity boundaries, and proper encapsulation of workflow-related concepts. 