# Timer Execution Enhancement

## Overview

The BBT Workflow Engine's timer execution system has been enhanced to provide the same flexibility as Dapr's job scheduling capabilities. Previously, the system only supported DateTime-based scheduling, but now supports multiple scheduling types including Cron expressions, Duration-based scheduling, and Immediate execution.

## What Changed

### Before (Limited to DateTime)

```csharp
public interface ITimerMapping
{
    Task<DateTime> Handler(ScriptContext context);
}

public class PaymentDueTimerRule : ITimerMapping
{
    public async Task<DateTime> Handler(ScriptContext context)
    {
        // Only DateTime return was possible
        return DateTime.UtcNow.AddDays(1);
    }
}
```

### After (Flexible Scheduling)

```csharp
public interface ITimerMapping
{
    Task<TimerSchedule> Handler(ScriptContext context);
}

public class PaymentDueTimerRule : ITimerMapping
{
    public async Task<TimerSchedule> Handler(ScriptContext context)
    {
        var frequency = context.Instance.Data.paymentFrequency?.ToString() ?? "monthly";
        
        return frequency switch
        {
            "daily" => TimerSchedule.FromCronExpression("0 9 * * *"),
            "weekly" => TimerSchedule.FromCronExpression("0 9 * * 1"),
            "monthly" => TimerSchedule.FromCronExpression("0 9 1 * *"),
            "immediate" => TimerSchedule.Immediate(),
            _ => TimerSchedule.FromDuration(TimeSpan.FromDays(30))
        };
    }
}
```

## New Components

### 1. TimerScheduleType Enum

```csharp
public enum TimerScheduleType
{
    DateTime = 1,    // Specific date/time
    Cron = 2,        // Cron expressions
    Duration = 3,    // Time span from now
    Immediate = 4    // Execute immediately
}
```

### 2. TimerSchedule Class

A value object that supports multiple scheduling types:

```csharp
var schedule1 = TimerSchedule.FromDateTime(DateTime.UtcNow.AddHours(2));
var schedule2 = TimerSchedule.FromCronExpression("0 9 * * *");
var schedule3 = TimerSchedule.FromDuration(TimeSpan.FromMinutes(30));
var schedule4 = TimerSchedule.Immediate();

// New Dapr-compatible methods
var schedule5 = TimerSchedule.FromExpression("@daily");
var schedule6 = TimerSchedule.Daily; // Predefined
var schedule7 = TimerSchedule.FromCronExpression(cronBuilder); // Fluent API
```

### 3. Enhanced Background Job Extensions

New extension methods that work with `TimerSchedule`:

```csharp
// Enhanced methods
await backgroundJobService.EnqueueTransitionTimerAsync(
    instanceId, flowName, domain, version, transitionKey, 
    TimerSchedule.FromCronExpression("0 9 * * *"), // Daily at 9 AM
    cancellationToken);

await backgroundJobService.EnqueueFlowTimeoutAsync(
    instanceId, flowName, domain, version,
    TimerSchedule.FromDuration(TimeSpan.FromHours(24)), // 24 hours from now
    cancellationToken);
```

## Benefits

### 1. Dapr Compatibility
- Direct integration with Dapr's job scheduling capabilities
- Support for all DaprJobSchedule types (DateTime, Cron, Duration)
- Seamless conversion to DaprJobSchedule when needed

### 2. Business Flexibility
- **Cron expressions** for recurring patterns (daily, weekly, monthly)
- **Duration-based** scheduling for relative timing
- **Immediate execution** for urgent processing
- **Absolute DateTime** for specific scheduling needs

### 3. Enhanced Use Cases

#### Recurring Payments
```csharp
TimerSchedule.FromCronExpression("0 9 1 * *") // Monthly on 1st at 9 AM
```

#### Business Hours Processing
```csharp
TimerSchedule.FromCronExpression("0 9 * * 1-5") // Weekdays at 9 AM
```

#### Priority-Based Processing
```csharp
return priority switch
{
    "urgent" => TimerSchedule.Immediate(),
    "high" => TimerSchedule.FromDuration(TimeSpan.FromMinutes(5)),
    "normal" => TimerSchedule.FromDuration(TimeSpan.FromHours(1)),
    _ => TimerSchedule.FromCronExpression("0 9 * * *")
};
```

## Backward Compatibility

The system maintains full backward compatibility:

1. **Existing DateTime returns** are automatically converted to `TimerSchedule.FromDateTime()`
2. **Legacy timer implementations** continue to work without changes
3. **TaskOrchestrationService** handles both DateTime and TimerSchedule responses

## Migration Path

### Immediate (No Changes Required)
- Existing code continues to work
- No breaking changes

### Recommended Enhancement
1. Update `ITimerMapping` implementations to return `TimerSchedule`
2. Use appropriate scheduling types for business requirements
3. Leverage Cron expressions for recurring patterns
4. Use Duration-based scheduling for relative timing

## Technical Implementation

### Updated Services
- `ITimerExecutionService` - Now returns `TimerSchedule`
- `ITaskOrchestrationService` - Enhanced `ExecuteTimerAsync` method
- `TimerExecutionService` - Updated implementation
- `StateMachineExecutor` - Works with new timer schedules

### New Extensions
- `WorkflowTimerJobExtensions` - Enhanced background job scheduling
- Automatic conversion to `DaprJobSchedule` for job execution

### Documentation
- Enhanced scripting engine documentation
- Comprehensive examples and migration guide
- Best practices for different scheduling types

## Common Patterns

### 1. Payment Processing
```csharp
// Monthly recurring payments
TimerSchedule.FromCronExpression("0 9 1 * *")

// Weekly payments 
TimerSchedule.FromCronExpression("0 9 * * 1")

// Immediate for urgent payments
TimerSchedule.Immediate()
```

### 2. Reminder Systems
```csharp
// Daily reminders at 10 AM
TimerSchedule.FromCronExpression("0 10 * * *")

// 3-day advance notice
TimerSchedule.FromDuration(TimeSpan.FromDays(3))
```

### 3. Batch Processing
```csharp
// Nightly batch processing
TimerSchedule.FromCronExpression("0 2 * * *")

// End of month processing
TimerSchedule.FromCronExpression("0 23 28-31 * *")
```

## Conclusion

The enhanced timer execution system provides powerful, flexible scheduling capabilities while maintaining simplicity and backward compatibility. Developers can now implement sophisticated timing logic that aligns with business requirements and takes full advantage of Dapr's job scheduling infrastructure.
