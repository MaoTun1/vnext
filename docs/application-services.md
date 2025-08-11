# Application Services

## Overview

The Application Layer orchestrates domain objects to fulfill use cases and contains application-specific business rules. It is organized into specialized modules, each with clear responsibilities and boundaries. The application layer follows **Command Query Responsibility Segregation (CQRS)** principles, separating read and write operations into distinct services for better scalability and maintainability.

## Modular Architecture

The Application Layer is organized into the following modules:

### 1. Orchestration Module (`BBT.Workflow.Orchestration`)

Coordinates task execution with support for both parallel and sequential execution strategies.

### 2. Execution Module

- **StateMachine (`BBT.Workflow.Execution.StateMachine`)**: State transition management
- **Tasks (`BBT.Workflow.Execution.Tasks`)**: Task execution services  
- **Rules (`BBT.Workflow.Execution.Rules`)**: Rule evaluation services

### 3. SubFlow Module (`BBT.Workflow.SubFlow`)

Manages SubFlow and SubProcess workflows, handling parent-child workflow relationships.

### 4. Extensions Module (`BBT.Workflow.Extensions`)

Handles instance extension operations and custom functionality.

### 5. Persistence Module (`BBT.Workflow.Tasks.Persistence`)

Provides pluggable persistence strategies for different task types.

### 6. Instances Module

Core instance management operations and administrative services.

## Core Application Services

### 1. IAdminAppService

Administrative service for workflow definition management and system operations.

```csharp
public interface IAdminAppService : IApplicationService
{
    Task PublishAsync(PublishInput input, CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(InvalidateCacheInput input, CancellationToken cancellationToken = default);
    Task ReInitializeAsync(CancellationToken cancellationToken = default);
}
```

#### PublishAsync Method

Publishes workflow definitions to the system with validation and versioning support.

```csharp
public async Task PublishAsync(PublishInput input, CancellationToken cancellationToken = default)
{
    // 1. Validate runtime schema
    runtimeInfoProvider.Check(input.Domain);
    
    if (!IsValidSchema(input))
    {
        throw new RuntimeSchemaInvalidException();
    }

    // 2. Switch to appropriate schema context
    using (currentSchema.Change(input.Flow))
    {
        // 3. Ensure database schema exists
        await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);
        
        // 4. Find or create instance
        var instance = await instanceRepository.FindByKeyAsync(input.Key, cancellationToken)
                       ?? Instance.Create(GuidGenerator.Create(), input.Flow, input.Key);

        // 5. Add tags and handle versioning
        instance.AddTags(input.Tags.ToArray());

        if (instance.IsTransient)
        {
            await SaveNewInstanceAsync(instance, input, cancellationToken);
        }
        else
        {
            await HandleExistingInstanceAsync(instance, input, cancellationToken);
        }
    }
}
```

**Usage Example:**
```csharp
var publishInput = new PublishInput
{
    Domain = "banking",
    Flow = "loan-approval",
    Key = "personal-loan-v2",
    Version = "2.1.0",
    Definition = workflowJson,
    Tags = new[] { "production", "loan", "personal" }
};

await adminService.PublishAsync(publishInput, cancellationToken);
```

#### InvalidateCacheAsync Method

Invalidates cached workflow components and reprocesses them.

```csharp
public async Task InvalidateCacheAsync(InvalidateCacheInput input, CancellationToken cancellationToken = default)
{
    runtimeInfoProvider.Check(input.Domain);
    
    using (currentSchema.Change(input.Flow))
    {
        var instance = await instanceRepository.FindByKeyAsync(input.Key, cancellationToken);
        if (instance?.LatestData == null)
        {
            throw new EntityNotFoundException(typeof(Instance), input.Key);
        }

        await castProcessor.ProcessAsync(
            input.Flow,
            new Reference(input.Key, input.Domain, input.Flow, input.Version ?? instance.LatestData.Version),
            instance.LatestData.Data.JsonElement,
            cancellationToken);
    }
}
```

#### ReInitializeAsync Method

Reloads all system components from runtime schemas.

```csharp
public async Task ReInitializeAsync(CancellationToken cancellationToken = default)
{
    var workflows = await GetSystemComponentsAsync<Workflow>(RuntimeSysSchemaInfo.Flows, cancellationToken);
    var tasks = await GetSystemComponentsAsync<WorkflowTask>(RuntimeSysSchemaInfo.Tasks, cancellationToken);
    var functions = await GetSystemComponentsAsync<Function>(RuntimeSysSchemaInfo.Functions, cancellationToken);
    // ... load other components

    await domainCacheContext.LoadAllAsync(new
    {
        workflows,
        tasks,
        functions,
        views,
        schemas,
        extensions
    }, cancellationToken);
}
```

## Orchestration Services

### TaskOrchestrationService (`BBT.Workflow.Orchestration`)

Service responsible for orchestrating workflow tasks with support for both parallel and sequential execution strategies.

```csharp
namespace BBT.Workflow.Orchestration;

public class TaskOrchestrationService : ITaskOrchestrationService
{
    public async Task ExecuteAsync(
        IEnumerable<OnExecuteTask> onExecuteTasks,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var tasks = onExecuteTasks.ToList();
        if (!tasks.Any()) return;

        // Check if tasks can be executed in parallel (no dependencies)
        var canExecuteInParallel = CanExecuteInParallel(tasks);

        if (canExecuteInParallel)
        {
            await ExecuteTasksInParallelAsync(tasks, instanceTransition, taskTrigger, context, cancellationToken);
        }
        else
        {
            await ExecuteTasksSequentiallyAsync(tasks, instanceTransition, taskTrigger, context, cancellationToken);
        }
    }

    public async Task<bool> ExecuteConditionAsync(
        ScriptCode script,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var task = ConditionTask.Create();
        var taskExecutor = taskExecutorFactory.GetExecutor(task.GetTaskType());

        var response = await taskExecutor.ExecuteAsync(task, script.DecodedCode, context, cancellationToken);
        return response as bool? ?? false;
    }
}
```

## Execution Services

### StateMachineExecutor (`BBT.Workflow.Execution.StateMachine`)

The main executor class that manages and executes state transitions in the workflow state machine.

```csharp
namespace BBT.Workflow.Execution.StateMachine;

public sealed class StateMachineExecutor : IStateMachineExecutor
{
    public async Task ExecuteTransitionAsync(
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        // 1. Create and persist transition record
        var instanceTransition = new InstanceTransition(
            guidGenerator.Create(),
            context.Instance.Id,
            context.Transition.Key,
            context.Instance.CurrentState!,
            new JsonData(JsonSerializer.Serialize(context.Body ?? new Dictionary<string, object>())),
            new JsonData(JsonSerializer.Serialize(context.Headers ?? new Dictionary<string, string>()))
        );

        await instanceTransitionRepository.InsertAsync(instanceTransition, true, cancellationToken);

        // 2. Execute transition lifecycle
        // 2.1 Transition OnExecutions
        await taskExecutionService.ExecuteAsync(
            context.Transition.OnExecutionTasks,
            instanceTransition,
            TaskTrigger.OnExecute,
            context,
            cancellationToken);

        // 2.2 Current state OnExits
        if (context.Workflow.GetState(context.Instance.CurrentState!).OnExits.Any())
        {
            await taskExecutionService.ExecuteAsync(
                context.Workflow.GetState(context.Instance.CurrentState!).OnExits,
                instanceTransition,
                TaskTrigger.OnExit,
                context,
                cancellationToken);
        }

        // 2.3 Target State OnEntries
        if (context.Workflow.GetState(context.Instance.CurrentState!).OnEntries.Any())
        {
            await taskExecutionService.ExecuteAsync(
                context.Workflow.GetState(context.Instance.CurrentState!).OnEntries,
                instanceTransition,
                TaskTrigger.OnEntry,
                context,
                cancellationToken);
        }

        // 3. Change state
        context.Instance.ChangeState(context.Transition);
        var targetState = context.Workflow.GetState(context.Instance.CurrentState!);

        // 4. Handle post-transition operations
        if (targetState.StateType != StateType.Finish)
        {
            // 4.1 Handle SubFlow/SubProcess
            if (targetState.StateType == StateType.SubFlow)
            {
                await HandleSubFlowAsync(context.Instance, targetState, context, cancellationToken);
            }

            // 4.2 Schedule delayed transitions
            await ScheduleTransitionsForLaterExecutionAsync(
                context.Instance, context.Workflow, cancellationToken);

            // 4.3 Execute automatic transitions
            await CheckAndExecuteAutomaticTransitionsAsync(
                context.Workflow, context.Instance, cancellationToken);
        }

        // 5. Handle instance status and complete transition
        await InstanceStatusHandleAsync(context.Instance, targetState, cancellationToken);
        instanceTransition.Completed(context.Instance.CurrentState!);
        await instanceTransitionRepository.UpdateAsync(instanceTransition, true, cancellationToken);
    }
}
```

### LocalTaskExecutor (`BBT.Workflow.Execution.Tasks`)

Handles local task execution with optimized task creation and proper instance isolation.

```csharp
namespace BBT.Workflow.Execution.Tasks;

public sealed class LocalTaskExecutor : ITaskOrchestrator
{
    public async Task ExecuteTaskAsync(
        OnExecuteTask onExecuteTask,
        InstanceTransition? instanceTransition,
        TaskTrigger taskTrigger,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        // Use TaskFactory for optimized task creation with proper isolation
        var task = await taskFactory.CreateExecutionTaskAsync(onExecuteTask.Task, cancellationToken);
        
        var taskExecutor = taskExecutorFactory.GetExecutor(task.GetTaskType());
        var instanceTask = new InstanceTask(
            guidGenerator.Create(),
            instanceTransition?.Id ?? guidGenerator.Create(),
            task.Key
        );

        // Get the appropriate persistence strategy based on TaskTrigger
        var persistenceStrategy = taskPersistenceStrategyFactory.GetStrategy(taskTrigger);
        
        try
        {
            var response = await taskExecutor.ExecuteAsync(
                task,
                onExecuteTask.Mapping.DecodedCode,
                context,
                cancellationToken);

            if (response != null)
            {
                // Use thread-safe operations for context updates
                lock (context.TaskResponse)
                {
                    var variableKey = task.Key.ToVariableName();
                    context.TaskResponse[variableKey] = response;
                }

                context.Instance.AddData(
                    guidGenerator.Create(),
                    new JsonData(JsonSerializer.Serialize(response, JsonSerializerConstants.JsonOptions)),
                    VersionStrategy.IncreasePatch
                );
            }

            instanceTask.Completed(
                new JsonData(JsonSerializer.Serialize(response, JsonSerializerConstants.JsonOptions)));
        }
        catch (Exception e)
        {
            instanceTask.Faulted(e.Message);
        }
        
        // Handle task completion persistence
        await persistenceStrategy.HandleCompletionAsync(instanceTask, cancellationToken);
    }
}
```

### RuleExecutionService (`BBT.Workflow.Execution.Rules`)

Service responsible for rule-based condition evaluation within workflow transitions.

```csharp
namespace BBT.Workflow.Execution.Rules;

public sealed class RuleExecutionService : IRuleExecutionService
{
    public async Task<bool> EvaluateConditionAsync(
        ScriptCode conditionScript,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        return await taskExecutionService.ExecuteConditionAsync(conditionScript, context, cancellationToken);
    }
}
```

## SubFlow Services

### SubFlowService (`BBT.Workflow.SubFlow`)

Service for managing SubFlow and SubProcess workflows, handling parent-child workflow relationships.

```csharp
namespace BBT.Workflow.SubFlow;

public sealed class SubFlowService : ISubFlowService
{
    public async Task HandleSubFlowAsync(
        Instance parentInstance,
        State targetState,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        if (targetState.SubFlow == null)
        {
            throw new InvalidOperationException(
                $"State \"{targetState.Key}\" is marked as SubFlow but has no SubFlow configuration.");
        }

        var subFlowConfig = targetState.SubFlow;

        // Load the sub-workflow definition
        var subWorkflow = await componentCacheStore.GetFlowAsync(
            subFlowConfig.Process.Domain,
            subFlowConfig.Process.Key,
            subFlowConfig.Process.Version,
            cancellationToken);

        // Handle SubProcess (Type "P") - still creates separate instance for parallel execution
        if (subFlowConfig.Type.Code == "P")
        {
            await HandleSubProcessAsync(parentInstance, targetState, subWorkflow, context, cancellationToken);
            return;
        }

        // Handle SubFlow (Type "S") - executes within the same instance
        await HandleSubFlowWithinInstanceAsync(parentInstance, targetState, subWorkflow, context, cancellationToken);
    }

    public async Task<bool> HasBlockingSubFlowsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var activeCorrelations = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instanceId, cancellationToken);

        return activeCorrelations.Any(c => 
            c.SubFlowType.Equals(SubFlowType.SubFlow) && 
            !c.IsCompleted);
    }
}
```

## Extension Services

### InstanceExtensionService (`BBT.Workflow.Extensions`)

Handles instance extension operations and custom functionality for workflow instances.

```csharp
namespace BBT.Workflow.Extensions;

public sealed class InstanceExtensionService : IInstanceExtensionService
{
    public async Task ExecuteExtensionAsync(
        Instance instance,
        ExtensionTask extensionTask,
        Dictionary<string, object>? input,
        CancellationToken cancellationToken = default)
    {
        // Build script context for extension execution
        var scriptContextBuilder = new ScriptContext.Builder()
            .SetInstance(instance)
            .SetBody(input);

        // Execute extension tasks without creating instance transitions
        await taskExecutionService.ExecuteAsync(
            new[] { OnExecuteTask.Create(extensionTask) },
            null, // No instance transition for extensions
            TaskTrigger.OnExecute,
            scriptContextBuilder.Build(),
            cancellationToken);
    }
}
```

## Persistence Services

### Task Persistence Strategies (`BBT.Workflow.Tasks.Persistence`)

Pluggable persistence strategies for different task execution scenarios.

#### StandardTaskPersistenceStrategy

```csharp
namespace BBT.Workflow.Tasks.Persistence.Strategies;

public sealed class StandardTaskPersistenceStrategy : ITaskPersistenceStrategy
{
    public bool CanHandle(TaskTrigger taskTrigger)
    {
        return taskTrigger is TaskTrigger.OnExecute or TaskTrigger.OnEntry or TaskTrigger.OnExit;
    }

    public async Task HandleCreationAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default)
    {
        await instanceTaskRepository.InsertAsync(instanceTask, true, cancellationToken);
    }

    public async Task HandleCompletionAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default)
    {
        await instanceTaskRepository.UpdateAsync(instanceTask, true, cancellationToken);
    }
}
```

#### ExtensionTaskPersistenceStrategy

```csharp
namespace BBT.Workflow.Tasks.Persistence.Strategies;

public sealed class ExtensionTaskPersistenceStrategy : ITaskPersistenceStrategy
{
    public bool CanHandle(TaskTrigger taskTrigger)
    {
        return taskTrigger == TaskTrigger.Extension;
    }

    public async Task HandleCreationAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default)
    {
        // Extensions typically don't persist task records during creation
        await Task.CompletedTask;
    }

    public async Task HandleCompletionAsync(InstanceTask instanceTask, CancellationToken cancellationToken = default)
    {
        // Only persist completed extension tasks for audit trail
        await instanceTaskRepository.InsertAsync(instanceTask, true, cancellationToken);
    }
}
```

## Instance Services (CQRS Pattern)

### IInstanceCommandAppService

Handles all write operations for workflow instances.

```csharp
public interface IInstanceCommandAppService : IApplicationService
{
    Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default);

    Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
}
```

### IInstanceQueryAppService

Handles all read operations for workflow instances.

```csharp
public interface IInstanceQueryAppService : IApplicationService
{
    Task<InstanceServiceResult<GetInstanceOutput>> GetInstanceAsync(
        GetInstanceInput input,
        CancellationToken cancellationToken = default);

    Task<InstanceServiceResponse<GetInstanceListOutput>> GetInstanceListAsync(
        GetInstanceListInput input,
        CancellationToken cancellationToken = default);

    Task<InstanceServiceResponse<GetInstanceHistoryOutput>> GetInstanceHistoryAsync(
        GetInstanceHistoryInput input,
        CancellationToken cancellationToken = default);
}
```

## Dependency Injection Configuration

```csharp
public static IServiceCollection AddApplicationModule(this IServiceCollection services)
{
    services.AddDomainModule();
    services.AddAetherApplication();

    // Core Instance Services
    services.AddScoped<IAdminAppService, AdminAppService>();
    services.AddScoped<IInstanceCommandAppService, InstanceCommandAppService>();
    services.AddScoped<IInstanceQueryAppService, InstanceQueryAppService>();

    // Orchestration Module
    services.AddScoped<ITaskOrchestrationService, TaskOrchestrationService>();

    // Execution Module
    services.AddScoped<IStateMachineExecutor, StateMachineExecutor>();
    services.AddScoped<IRuleExecutionService, RuleExecutionService>();

    // SubFlow Module
    services.AddScoped<ISubFlowService, SubFlowService>();

    // Extensions Module
    services.AddScoped<IInstanceExtensionService, InstanceExtensionService>();

    // Persistence Module
    services.AddScoped<ITaskPersistenceStrategy, StandardTaskPersistenceStrategy>();
    services.AddScoped<ITaskPersistenceStrategy, ExtensionTaskPersistenceStrategy>();
    services.AddScoped<ITaskPersistenceStrategyFactory, TaskPersistenceStrategyFactory>();

    // Task Execution
    services.AddScoped<ITaskExecutorFactory, TaskExecutorFactory>();
    
    return services;
}
```

## Module Benefits

### 1. Clear Separation of Concerns
Each module has a specific responsibility, making the codebase easier to understand and maintain.

### 2. Enhanced Testability
Modules can be tested independently with clear interfaces and dependencies.

### 3. Improved Scalability
Different modules can be optimized independently based on their specific performance characteristics.

### 4. Better Team Collaboration
Teams can work on different modules with minimal conflicts and clear boundaries.

### 5. Flexible Deployment
Modules provide a foundation for potential future microservice decomposition if needed.

This modular architecture ensures maintainable, scalable, and well-organized application services while following established architectural patterns and best practices. 