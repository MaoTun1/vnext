# Auto Transition Condition Evaluation

## Overview

This document describes the auto transition condition evaluation system implemented in the workflow engine. The system evaluates automatic transition conditions within the pipeline step rather than in the PreHandler, using the Result pattern for exception-free error handling.

## Motivation

Previously, automatic transition conditions were evaluated in the PreHandler, and when a condition was false, an exception was thrown. This approach had several issues:

1. **Exception misuse**: A false condition is a normal business outcome, not an error condition
2. **Performance**: Exception handling is expensive
3. **Logic placement**: PreHandler only knows about a single transition, making it difficult to implement "at least one must succeed" logic
4. **Unclear semantics**: Using exceptions for flow control obscures the actual business logic

## Architecture

### Domain Layer

#### AutoConditionStatus Enum

```csharp
public enum AutoConditionStatus
{
    Satisfied,      // Condition is true, transition can execute
    NotSatisfied,   // Condition is false (normal business outcome)
    Failed          // Technical error during evaluation
}
```

#### AutoConditionEvaluation Struct

A readonly record struct that encapsulates the evaluation result:

```csharp
public readonly record struct AutoConditionEvaluation
{
    public string TransitionKey { get; init; }
    public AutoConditionStatus Status { get; init; }
    public Error? Error { get; init; }
    public bool IsSuccess => Status == AutoConditionStatus.Satisfied;
    public bool IsFailed => Status == AutoConditionStatus.Failed;
}
```

**Location**: `src/BBT.Workflow.Domain/Execution/Transitions/Evaluation/`

### Application Layer

#### IAutoConditionEvaluator Interface

Service interface for evaluating automatic transition conditions:

```csharp
public interface IAutoConditionEvaluator
{
    Task<Result<AutoConditionEvaluation>> EvaluateAsync(
        Transition transition,
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default);
}
```

#### AutoConditionEvaluator Implementation

Concrete implementation that:
- Uses `ITaskConditionService` to execute condition scripts
- Uses `IScriptContextFactory` to build script contexts
- Returns structured evaluation results using the Result pattern
- Logs evaluation outcomes and errors
- Caches ScriptContext in the TransitionExecutionContext

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Evaluation/`

#### RunAutomaticTransitionsStep

Updated pipeline step that:
1. Evaluates all automatic transitions for the target state
2. Finds the first satisfied transition
3. Validates that at least one transition is satisfied
4. Enqueues the winning transition for inline execution

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Pipeline/Steps/`

## Behavior

### Evaluation Flow

1. When a state has automatic transitions, `RunAutomaticTransitionsStep` evaluates each one
2. For each transition:
   - If the rule is missing → **Fail** with validation error
   - If evaluation succeeds → Return **Satisfied** or **NotSatisfied** based on condition result
   - If a technical error occurs → **Fail** with technical error
3. After all evaluations:
   - If at least one is **Satisfied** → Enqueue the first satisfied transition
   - If all are **NotSatisfied** → Fail with validation error
   - If any **Failed** → Fail immediately with the error

### Error Handling

The system distinguishes three scenarios:

| Scenario | Status | Result | HTTP Status |
|----------|--------|--------|-------------|
| Condition is true | Satisfied | Success, transition executes | N/A |
| Condition is false | NotSatisfied | Normal business outcome | 400 (if all fail) |
| Script error, missing rule, etc. | Failed | Technical error | 500 |

## Key Benefits

1. **Clear semantics**: False conditions are not errors, they're business outcomes
2. **Better performance**: No exception throwing for normal flow
3. **Improved logging**: Structured logging of evaluation outcomes
4. **Result pattern**: Exception-free error handling
5. **Proper separation**: Business logic (which transition) vs technical concerns (how to evaluate)
6. **Testability**: Easier to unit test without catching exceptions

## Usage Example

```csharp
// In RunAutomaticTransitionsStep
foreach (var automaticTransition in context.Target.AutoTransitions)
{
    var evalResult = await _autoConditionEvaluator.EvaluateAsync(
        automaticTransition,
        context,
        cancellationToken);

    if (!evalResult.IsSuccess)
    {
        // Technical error - fail pipeline
        return Result<StepOutcome>.Fail(evalResult.Error);
    }

    evaluations.Add(evalResult.Value);
}

// Find first satisfied transition
var winner = evaluations.FirstOrDefault(e => e.Status == AutoConditionStatus.Satisfied);

if (winner.TransitionKey is null)
{
    // All NotSatisfied - business rule violation
    return Result<StepOutcome>.Fail(
        Error.Validation("auto.none.satisfied", "..."));
}

// Enqueue winning transition
context.Directives.EnqueueInlineAuto(command);
```

## Dependency Injection

The `IAutoConditionEvaluator` service is registered in the Application module:

```csharp
// In WorkflowApplicationModuleServiceCollectionExtensions.AddTransitionPipeline()
services.AddScoped<IAutoConditionEvaluator, AutoConditionEvaluator>();
```

## Testing Considerations

When testing automatic transitions:

1. **Test all three outcomes**: Satisfied, NotSatisfied, and Failed
2. **Test multiple transitions**: Ensure first satisfied wins
3. **Test all NotSatisfied**: Ensure proper error message
4. **Test technical errors**: Script compilation errors, missing rules, etc.
5. **Verify logging**: Check that appropriate log levels are used

## Migration Notes

### PreHandler Changes

**Important**: No changes are required in the `AutomaticTransitionHandler.PreHandle` method. The condition evaluation logic has been completely moved to `RunAutomaticTransitionsStep`.

### Backward Compatibility

The change is backward compatible:
- Existing workflows continue to work
- The only behavioral difference is error handling (no exceptions for false conditions)
- All business logic remains the same

## Performance Impact

Expected performance improvements:
- **No exceptions**: Eliminates exception construction and stack unwinding for false conditions
- **Cached ScriptContext**: ScriptContext is created once and reused
- **Structured logging**: Better performance than exception-based logging

## Related Documentation

- [Transition Pipeline Architecture](transition-pipeline-architecture.md)
- [Result Pattern](../aether/Results/result-pattern/README.md)
- [Scripting Engine](scripting-engine.md)
- [Task Executors](task-executors.md)

