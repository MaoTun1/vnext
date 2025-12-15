# Result Pattern & Railway Programming

## Overview

The BBT Workflow Engine uses the **Result Pattern** from Aether SDK for exception-free error handling. Combined with **Railway Programming** (also known as Railway Oriented Programming), this approach provides a clean, composable way to handle errors without throwing exceptions in business logic.

## Core Concepts

### Result Pattern

The Result pattern encapsulates the outcome of an operation that can either succeed or fail:

```csharp
using BBT.Aether.Results;

// Success case
Result<TransitionOutput> success = Result<TransitionOutput>.Ok(output);

// Failure case
Result<TransitionOutput> failure = Result<TransitionOutput>.Fail(
    Error.Validation("code", "message"));

// Non-generic Result for void operations
Result result = Result.Ok();
Result error = Result.Fail(Error.NotFound("entity.not.found", "Entity not found"));
```

### Result Properties

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }        // Only valid when IsSuccess is true
    public Error Error { get; }     // Only valid when IsSuccess is false
}
```

### Error Types

Aether SDK provides factory methods for creating typed errors:

```csharp
// Validation errors (400 Bad Request)
Error.Validation("validation.failed", "Validation failed", additionalData);

// Not found errors (404 Not Found)
Error.NotFound("entity.not.found", "Entity was not found");

// Conflict errors (409 Conflict)
Error.Conflict("version.conflict", "Version conflict detected");

// Failure errors (500 Internal Server Error)
Error.Failure("operation.failed", "Operation failed unexpectedly");
```

## Railway Programming

Railway Programming treats success and failure as two parallel "tracks". Operations are chained together, and if any operation fails, subsequent operations are skipped and the error propagates through the chain.

```
    Success Track ─────────────────────────────────────────────────►
                          ↓                 ↓                 ↓
                     [Operation A]    [Operation B]    [Operation C]
                          ↓                 ↓                 ↓
    Failure Track ─────────────────────────────────────────────────►
```

## Extension Methods

### BindAsync

Chains async operations that return `Result<T>`. If the previous result is a failure, the operation is skipped.

```csharp
public Task<Result<TransitionOutput>> ExecuteTransitionAsync(
    WorkflowExecutionContext context,
    CancellationToken cancellationToken = default)
{
    return GetExecutionStrategy(context.Mode)
        .BindAsync(strategy => ExecuteStrategyAsync(strategy, context, cancellationToken))
        .BindAsync(execCtx => BuildTransitionOutputAsync(context, execCtx, cancellationToken));
}
```

### ThenAsync

Similar to `BindAsync` but for synchronous Result-returning operations:

```csharp
public Task<Result<TransitionExecutionContext>> CreateAsync(
    WorkflowExecutionContext input,
    CancellationToken cancellationToken)
{
    return ValidateDomain(input.Domain)
        .BindAsync(_ => RehydrateInstanceAsync(input, cancellationToken))
        .ThenAsync(data => Task.FromResult(ResolveStateAndTransition(data, input)))
        .MapAsync(data => BuildExecutionContext(data, input));
}
```

### MapAsync

Transforms the success value without changing the Result type:

```csharp
return await Result.Ok(context)
    .Map(CreateTransitionInput)
    .BindAsync(input => ForwardToSubflowAsync(context, input, cancellationToken))
    .Map(result => CreateStepOutcome(context, result));
```

### Tap & TapAsync

Performs side effects without changing the Result. Useful for logging, metrics, or state updates:

```csharp
return await Result.Ok(BuildStateTransitionInfo(context))
    .Tap(info => RecordTransitionMetric(context, info))
    .TapAsync(info => PerformStateChangeAsync(context, info, cancellationToken))
    .ThenAsync(_ => UpdateTargetStateInContext(context))
    .MapAsync(_ => StepOutcome.Continue());
```

### OnSuccess

Executes a side effect only when the result is successful:

```csharp
return await Result.Ok(BuildStateTransitionInfo(context))
    .Tap(info => RecordTransitionMetric(context, info))
    .TapAsync(info => PerformStateChangeAsync(context, info, cancellationToken))
    .ThenAsync(_ => UpdateTargetStateInContext(context))
    .OnSuccess(_ => RecordStateEntryMetric(context))
    .OnSuccess(_ => LogStateChange(context))
    .OnSuccess(_ => AddTelemetryEvent(context))
    .MapAsync(_ => StepOutcome.Continue());
```

## ResultExtensions.TryAsync

Wraps potentially throwing operations in a Result:

```csharp
var result = await ResultExtensions.TryAsync(async ct =>
{
    var scriptRunner = await _scriptEngine.CompileToInstanceAsync<IMapping>(
        mapping.DecodedCode,
        cancellationToken: ct);

    await scriptRunner.InputHandler(task, context.ScriptContext);
}, cancellationToken, ex => Error.Failure(
    WorkflowErrorCodes.TaskExecution,
    $"DaprBinding task input handler failed: {ex.Message}"));

if (!result.IsSuccess)
{
    Logger.TaskInputHandlerFailed(
        task.Key,
        TaskType.ToString(),
        context.ScriptContext.Instance.Id,
        result.Error.Message ?? "Unknown error");
}

return result;
```

### TryAsync with Return Value

```csharp
var result = await ResultExtensions.TryAsync<object?>(async ct =>
{
    var scriptRunner = await _scriptEngine.CompileToInstanceAsync<IMapping>(
        mapping.DecodedCode,
        cancellationToken: ct);

    var outputResponse = await scriptRunner.OutputHandler(context.ScriptContext);
    return outputResponse.Data;
}, cancellationToken, ex => Error.Failure(
    WorkflowErrorCodes.TaskExecution,
    $"Output handler failed: {ex.Message}"));
```

## Error Handling Patterns

### Workflow Error Constants

Define domain-specific error codes in a centralized location:

```csharp
public static class WorkflowErrorCodes
{
    public const string InvalidWorkflow = "workflow.invalid";
    public const string ConflictWorkflow = "workflow.conflict";
    public const string TaskExecution = "task.execution.failed";
    public const string UnsupportedTaskType = "task.type.unsupported";
    public const string TaskBindingMappingFailed = "task.binding.mapping.failed";
}
```

### Workflow Error Factory

Create typed errors with factory methods:

```csharp
public static class WorkflowErrors
{
    public static Error DomainValidationFailed(string domain, string message)
        => Error.Validation("domain.validation.failed", 
            $"Domain validation failed for '{domain}': {message}");

    public static Error InstanceNotFound(string key)
        => Error.NotFound("instance.not.found", 
            $"Instance with key '{key}' was not found");

    public static Error InstanceAlreadyExists(string key)
        => Error.Conflict("instance.already.exists", 
            $"Instance with key '{key}' already exists");

    public static Error WorkflowVersionConflict()
        => Error.Conflict("workflow.version.conflict", 
            "Workflow version conflict detected");
}
```

## Pipeline Step Pattern

All pipeline steps return `Result<StepOutcome>` for consistent error handling:

```csharp
public sealed class ChangeStateStep : ITransitionStep
{
    public int Order => LifecycleOrder.ChangeState;

    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Skip for SubFlow resume - state already changed
        if (context.Directives.IsSubFlowResume || context.Transition == null)
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway Oriented Programming: Fluent chain
        return await Result.Ok(BuildStateTransitionInfo(context))
            .Tap(info => RecordTransitionMetric(context, info))
            .TapAsync(info => PerformStateChangeAsync(context, info, cancellationToken))
            .ThenAsync(_ => UpdateTargetStateInContext(context))
            .OnSuccess(_ => RecordStateEntryMetric(context))
            .OnSuccess(_ => LogStateChange(context))
            .OnSuccess(_ => AddTelemetryEvent(context))
            .MapAsync(_ => StepOutcome.Continue());
    }
}
```

## Service Layer Pattern

Application services use Railway Programming for clean error propagation:

```csharp
public sealed class WorkflowExecutionService : IWorkflowExecutionService
{
    [UnitOfWork]
    [Log]
    [Trace]
    public Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        [Enrich] WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return GetExecutionStrategy(context.Mode)
            .BindAsync(strategy => ExecuteStrategyAsync(strategy, context, cancellationToken))
            .BindAsync(execCtx => BuildTransitionOutputAsync(context, execCtx, cancellationToken));
    }

    private Result<ITransitionStrategy> GetExecutionStrategy(ExecMode mode)
        => execFactory.Get(mode);

    private Task<Result<TransitionExecutionContext>> ExecuteStrategyAsync(
        ITransitionStrategy strategy,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
        => strategy.ExecuteAsync(context, cancellationToken);
}
```

## Best Practices

### 1. Use Result for Business Operations

```csharp
// Good: Returns Result for expected failures
public Result<Instance> CreateInstance(CreateInstanceInput input)
{
    if (string.IsNullOrEmpty(input.Key))
        return Result<Instance>.Fail(Error.Validation("key.required", "Key is required"));
    
    return Result<Instance>.Ok(Instance.Create(input));
}

// Avoid: Throwing exceptions for expected failures
public Instance CreateInstance(CreateInstanceInput input)
{
    if (string.IsNullOrEmpty(input.Key))
        throw new ValidationException("Key is required"); // Don't do this
    
    return Instance.Create(input);
}
```

### 2. Chain Operations Fluently

```csharp
// Good: Fluent Railway chain
return ValidateDomain(input.Domain)
    .BindAsync(_ => RehydrateInstanceAsync(input, cancellationToken))
    .ThenAsync(data => ResolveStateAndTransition(data, input))
    .MapAsync(data => BuildExecutionContext(data, input));

// Avoid: Explicit if-checks
var domainResult = ValidateDomain(input.Domain);
if (!domainResult.IsSuccess)
    return Result<TransitionExecutionContext>.Fail(domainResult.Error);

var rehydrateResult = await RehydrateInstanceAsync(input, cancellationToken);
if (!rehydrateResult.IsSuccess)
    return Result<TransitionExecutionContext>.Fail(rehydrateResult.Error);
// ... more checks
```

### 3. Use TryAsync for External Calls

```csharp
// Good: Wrap external calls with TryAsync
var result = await ResultExtensions.TryAsync(async ct =>
{
    return await externalService.CallAsync(request, ct);
}, cancellationToken, ex => Error.Failure("external.call.failed", ex.Message));

// Avoid: Try-catch blocks everywhere
try
{
    return Result<Response>.Ok(await externalService.CallAsync(request, cancellationToken));
}
catch (Exception ex)
{
    return Result<Response>.Fail(Error.Failure("external.call.failed", ex.Message));
}
```

### 4. Use Tap for Side Effects

```csharp
// Good: Side effects with Tap
return await Result.Ok(entity)
    .TapAsync(e => repository.UpdateAsync(e, cancellationToken))
    .Tap(_ => logger.LogInformation("Entity updated"))
    .Map(_ => BuildResponse());

// Avoid: Breaking the chain for side effects
var entity = ...;
await repository.UpdateAsync(entity, cancellationToken);
logger.LogInformation("Entity updated");
return Result<Response>.Ok(BuildResponse());
```

### 5. Preserve Error Context

```csharp
// Good: Include context in errors
return Result<TaskInvocationResult>.Fail(Error.Failure(
    WorkflowErrorCodes.TaskExecution,
    $"Task '{taskKey}' of type '{taskType}' failed: {ex.Message}",
    new { TaskKey = taskKey, TaskType = taskType }));

// Avoid: Generic error messages
return Result<TaskInvocationResult>.Fail(Error.Failure("error", "Something went wrong"));
```

## Integration with OpenTelemetry

Result pattern integrates seamlessly with tracing:

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

## Related Documentation

- [Aether SDK Aspects](./aether-sdk-aspects.md) - Cross-cutting concerns with attributes
- [Transition Pipeline Architecture](./transition-pipeline-architecture.md) - Pipeline step implementation
- [Task Executors](./task-executors.md) - Task execution with Result pattern

