# OpenTelemetry Logging Architecture

## Overview

vNext workflow engine uses OpenTelemetry for distributed tracing and structured logging. Telemetry is now provided by the **Aether SDK** (`BBT.Aether.Telemetry`), with workflow-specific extensions for logging and tracing.

> **Note**: Telemetry infrastructure is now managed by Aether SDK. This document describes the workflow-specific logging patterns and conventions.

## Architecture Components

### 1. Aether SDK Telemetry

Telemetry is configured using Aether SDK's `AddAetherTelemetry`:

```csharp
services.AddAetherTelemetry(configuration);
```

This provides:
- OpenTelemetry tracing with automatic instrumentation
- Structured logging with enrichment
- OTLP export to collectors
- Automatic trace context propagation

### 2. WorkflowLogs (Source-Generated)

High-performance, zero-allocation logging methods using C# source generators:

Located: `BBT.Workflow.Domain/Logging/WorkflowLogs.cs`

Categories of log messages:
- **Transition Execution**: State changes, transition enqueuing, rule failures
- **Task Execution**: Task failures, input/output handler errors
- **SubFlow**: SubFlow events, correlation, completion
- **Instance Management**: Instance lifecycle, locks, validation
- **Background Jobs**: Job completion, failures, cancellations

### 3. ActivityExtensions

Helper extensions for OpenTelemetry Activity operations:

Located: `BBT.Workflow.Domain/Logging/ActivityExtensions.cs`

```csharp
// Set display name for spans
activity?.SetDisplayName($"[{Order}] {nameof(RunAutomaticTransitionsStep)}");

// Record exception with error status
activity?.RecordExceptionWithStatus(exception, "Description");
```

### 4. EventId Strategy

Structured EventId numbering scheme:
- **10xxx**: Execution layer
- **20xxx**: Orchestration layer
- **30xxx**: Infrastructure layer
- **40xxx**: Application layer
- **50xxx**: Domain layer

Within each range:
- **xx01-xx39**: Information level
- **xx40-xx69**: Warning level
- **xx70-xx99**: Error level

## Configuration

### Aether SDK Telemetry Setup

Telemetry is configured using Aether SDK:

```csharp
services.AddAetherTelemetry(configuration);
```

### Environment Variables

Standard OpenTelemetry environment variables are supported:

- **`OTEL_SERVICE_NAME`**: Service name for telemetry
- **`OTEL_EXPORTER_OTLP_ENDPOINT`**: OTLP endpoint URL
- **`OTEL_EXPORTER_OTLP_PROTOCOL`**: Protocol (`grpc` or `http/protobuf`)
- **`ASPNETCORE_ENVIRONMENT`**: Environment name

**Example launchSettings.json:**
```json
{
  "profiles": {
    "http": {
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "OTEL_SERVICE_NAME": "vnext-app",
        "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4318",
        "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf"
      }
    }
  }
}
```

### appsettings.json

Refer to Aether SDK documentation for telemetry configuration options.

### Log Levels (Standard .NET Configuration)

Log levels are configured using the standard .NET `Logging:LogLevel` section:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "BBT.Workflow": "Debug"
    }
  }
}
```

## Usage Patterns

### 1. Using WorkflowLogs Extension Methods

All workflow logging uses source-generated extension methods from `WorkflowLogs`:

```csharp
using BBT.Workflow.Logging;

// State change logging
logger.StateChanged(fromState, toState, instanceId);

// Task execution logging
logger.TaskExecutionFailed(ex, taskKey, taskType, instanceId);
logger.TaskInputHandlerFailed(taskKey, taskType, instanceId, errorMessage);
logger.TaskOutputHandlerFailed(taskKey, taskType, instanceId, errorMessage);

// SubFlow logging
logger.SubFlowEventReceived(subInstanceId, parentInstanceId, domain, flow);
logger.SubFlowCorrelationCompleted(subInstanceId, parentInstanceId);
logger.SubFlowCompletionFailed(ex, subInstanceId, parentInstanceId);

// Transition logging
logger.TransitionRuleFailed(transitionKey, instanceId, reason);
logger.AutoTransitionSelected(transitionKey, stateKey, instanceId);

// Job logging
logger.JobCompleted(jobName, transitionKey, instanceId);
logger.JobFailed(ex, jobName, instanceId);
```

### 2. Using ActivityExtensions for Tracing

```csharp
using BBT.Workflow.Logging;
using System.Diagnostics;

// Set display name for pipeline steps
Activity.Current?.SetDisplayName($"[{Order}] {nameof(RunAutomaticTransitionsStep)}");

// Record exceptions with error status
Activity.Current?.RecordExceptionWithStatus(exception, "Task execution failed");
```

## Log Schema

All logs follow a consistent schema:

```json
{
  "ts": "2025-10-11T10:30:45.123Z",
  "level": "Information",
  "message": "State changed from Draft to Submitted for instance 8b3e...",
  "event.id": 10003,
  "event.name": "StateChanged",
  "category": "BBT.Workflow.Execution.Pipeline.Steps.ChangeStateStep",
  "class": "ChangeStateStep",
  "method": "ExecuteAsync",
  "domain": "amorphie",
  "flow": "loan-approval",
  "instanceId": "8b3e4a5c-...",
  "transitionKey": "submit",
  "http.header.x-correlation-id": "abc123...",
  "http.header.x-request-id": "def456...",
  "environment": "development",
  "team": "workflow-team",
  "traceId": "4d5e6f...",
  "spanId": "1a2b3c..."
}
```

## Filtering and Querying

### Filter by Instance

```
instanceId:"8b3e4a5c-1234-5678-90ab-cdef12345678"
```

### Filter by Workflow

```
domain:"amorphie" AND flow:"loan-approval"
```

### Filter by Event Type

```
event.id:[10001 TO 10099]  // All execution layer events
event.id:[10070 TO 10099]  // Only execution errors
event.name:"TransitionStarted"  // Specific event type
```

### Filter by Custom Attributes

```
team:"workflow-team" AND environment:"production"
http.header.x-correlation-id:"abc123"
```

### Exclude Infrastructure Logs

To focus on business logic logs and exclude infrastructure noise:
```
NOT category:Microsoft.* AND NOT category:System.* AND category:BBT.Workflow.*
```

## Best Practices

### 1. Always Use Scopes for Context

Create a scope at the beginning of major operations:
```csharp
using var scope = _logger.ForTransition(...);
```

### 2. Use Source-Generated Methods

Prefer source-generated methods over manual LogInformation:
```csharp
// Good - use WorkflowLogs extension methods
_logger.StateChanged(fromState, toState, instanceId);
_logger.TransitionRuleFailed(transitionKey, instanceId, reason);

// Avoid - manual string interpolation
_logger.LogInformation("State changed from {From} to {To}", fromState, toState);
```

### 3. Include Timing Information

Always measure and log execution time for significant operations:
```csharp
var sw = Stopwatch.StartNew();
// ... operation ...
sw.Stop();
_logger.OperationCompleted(..., sw.ElapsedMilliseconds);
```

### 4. Appropriate Log Levels

- **Trace**: Very detailed debugging (disabled in production)
- **Debug**: Detailed flow information (pipeline steps, state changes)
- **Information**: Significant business events (transition started/completed)
- **Warning**: Recoverable errors, rule failures, retries
- **Error**: Unrecoverable errors requiring attention

### 6. Use Trace Context for Debugging

When debugging production issues:
- Check the API response headers for `X-Trace-Id`
- Use this ID to find the complete trace in your observability tool
- All logs for that request will have the same `traceId`
- This enables end-to-end request tracking across services

Example debugging workflow:
```bash
# 1. Call the API and capture headers
curl -i https://api.example.com/api/v1/instances/123/transitions/approve

# 2. Note the trace ID from response
X-Trace-Id: abc123...

# 3. Search logs in Grafana/OpenObserve
{traceId="abc123..."}

# 4. Search traces in Jaeger/Tempo
Trace ID: abc123...
```

### 7. Filter Trace Noise

Keep traces clean and focused on business logic:
- ✅ Enable `EnableExcludedPaths: true` (default)
- ✅ Configure `ExcludedPaths` to filter infrastructure endpoints
- ✅ Filter health checks, metrics, and internal endpoints
- ❌ Don't include infrastructure noise in production traces
- 🎯 Focus: Business transactions and user journeys

Example for clean production traces:
```json
{
  "Telemetry": {
    "Tracing": {
      "EnableExcludedPaths": true
    },
    "Logging": {
      "ExcludedPaths": [
        "^/swagger(?:/.*)?$",
        "^/dapr(?:/.*)?$",
        "^/health$",
        "^/healthz$",
        "^/metrics$",
        "^/internal/",
        "^/actuator",
        "^/dapr.proto.runtime",
        "^/v1.0/lock",
        "^/v1.0/unlock"
      ]
    }
  }
}
```

### 8. PII and Sensitive Data

Never log sensitive information:
- Credit card numbers
- Passwords
- Personal identification numbers
- Full customer data

Use data masking when necessary.

## Integration with Observability Stack

### OpenTelemetry Collector

Logs are exported via OTLP protocol to the OpenTelemetry Collector configured at `http://localhost:4318`.

### OpenObserve/Loki/ELK

The collector forwards logs to your observability backend (OpenObserve, Loki, or ELK).

### Grafana Dashboards

Create dashboards to visualize:
- Error rates by prefix/domain/flow
- Transition execution times
- Pipeline step performance
- Task failure rates
- Instance lifecycle tracking

### Tracing Integration

All logs are automatically correlated with distributed traces through `traceId` and `spanId`, enabling:
- End-to-end transaction tracing
- Cross-service request correlation
- Performance bottleneck identification

#### Dapr Operations in Traces

**Problem:** Dapr internal operations (lock/unlock) appear in traces without span names, creating noise.

**Solution:** Automatic filtering with `FilterDaprInternalOperations: true` (default)

**Before filtering:**
```
Trace Timeline:
├─ POST /api/v1/instances/123/transitions/approve (200ms)
│  ├─ Transition Pipeline (180ms)
│  │  ├─ ValidateTransition (5ms)
│  │  ├─ [no span name] (10ms) ← Dapr Lock (noise)
│  │  ├─ ExecuteTasks (150ms)
│  │  └─ [no span name] (5ms) ← Dapr Unlock (noise)
│  └─ SaveChanges (20ms)
```

**After filtering:**
```
Trace Timeline:
├─ POST /api/v1/instances/123/transitions/approve (200ms)
│  ├─ Transition Pipeline (180ms)
│  │  ├─ ValidateTransition (5ms)
│  │  ├─ ExecuteTasks (150ms)
│  │  └─ SaveChanges (20ms)
```

**To include Dapr operations** (for debugging):
```json
{
  "Telemetry": {
    "Tracing": {
      "EnableExcludedPaths": false
    }
  }
}
```

When path filtering is disabled, all Dapr operations will appear in traces. You can also selectively include specific paths by removing them from the `ExcludedPaths` array.

#### Using Trace IDs from API Responses

Every API response includes trace context headers. To trace a specific request:

1. **Call your API**:
   ```bash
   curl -i https://api.example.com/api/v1/workflows/execute
   ```

2. **Copy the X-Trace-Id from response headers**:
   ```
   X-Trace-Id: 4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a
   ```

3. **Search in your trace viewer**:
   - **Jaeger**: Go to Search → Trace ID → Paste the value
   - **Grafana Tempo**: Use query `{trace_id="4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a"}`
   - **Zipkin**: Use the search box with the trace ID

4. **View the complete trace** showing:
   - All services involved
   - Timing for each operation
   - Logs correlated to specific spans
   - Any errors or warnings

## Migration from Old Logging

### Before
```csharp
_logger.LogDebug("Starting transition {TransitionKey}", key);
_logger.LogInformation("State changed from {From} to {To}", fromState, toState);
```

### After
```csharp
// Use source-generated WorkflowLogs extension methods
_logger.StateChanged(fromState, toState, instanceId);
_logger.TransitionRuleFailed(transitionKey, instanceId, reason);
_logger.TaskExecutionFailed(ex, taskKey, taskType, instanceId);
```

Benefits:
- Structured, queryable fields
- Automatic enrichment with class/method via VNextLogEnricherProcessor
- Trace correlation via OpenTelemetry
- Zero-allocation performance with LoggerMessage source generation
- Consistent schema across the system
- Clean log messages without prefix noise

## Performance Considerations

1. **Source Generators**: LoggerMessage source generators eliminate boxing and string interpolation overhead
2. **Scope Reuse**: Scopes automatically propagate metadata to all nested logs
3. **Conditional Logging**: Debug and Trace logs are automatically filtered in production
4. **Batch Export**: OTLP exporter batches logs for efficient transmission

## Troubleshooting

### No Logs Appearing in OpenTelemetry/Grafana/Jaeger

1. **Check if OpenTelemetry logging is enabled**:
   ```json
   "Telemetry": {
     "Logging": {
       "Enabled": true  // Must be true
     }
   }
   ```

2. **Verify OTLP exporter is enabled**:
   ```json
   "Telemetry": {
     "Logging": {
       "EnableOtlpExporter": true  // Must be true
     }
   }
   ```

3. **Check OTLP endpoint is reachable**:
   ```bash
   curl http://localhost:4318/v1/logs
   ```

4. **Verify log level configuration** in `Logging:LogLevel` section

5. **Ensure OpenTelemetry Collector is running**:
   ```bash
   docker ps | grep otel-collector
   ```

6. **Check if logs appear in console** (if `EnableConsoleExporter: true`):
   - If logs appear in console but not in observability backend, the issue is with the collector or backend
   - If logs don't appear in console either, check log level configuration

### Missing Enrichment Fields

1. Verify `VNextLogEnricherProcessor` is registered
2. Check scope is created using `LogScope` extensions
3. Ensure `IncludeScopes` is `true` in configuration

### High Log Volume

1. Increase log level to `Warning` or `Error` in the standard `Logging:LogLevel` configuration
2. Use `ExcludedPaths` in Telemetry configuration to filter health checks, metrics, and other noisy endpoints
3. Add regex patterns to exclude specific API routes that generate too many logs
4. Configure granular log levels per namespace
5. Implement sampling for high-frequency operations
6. Use aggregated metrics instead of detailed logs

Example configuration for production:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "BBT.Workflow": "Information",
      "BBT.Workflow.Execution": "Information",
      "Microsoft": "Error",
      "System": "Error"
    }
  },
  "Telemetry": {
    "Logging": {
      "ExcludedPaths": [
        "^/health.*",
        "^/metrics.*",
        "^/swagger.*",
        "^/api/.*/status$"
      ]
    }
  }
}
```

## Distributed Tracing

### Tracing with Aether SDK Aspects

Pipeline steps use the `[Trace]` aspect from Aether SDK for automatic span creation:

```csharp
using BBT.Aether.Aspects;

public sealed class RunAutomaticTransitionsStep : ITransitionStep
{
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(
        TransitionExecutionContext context, 
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(RunAutomaticTransitionsStep)}");
        // ...
    }
}
```

### Span Hierarchy

Typical trace for a transition execution:

```
HTTP Request (ASP.NET Core)
└── Transition Pipeline
    ├── [1] ValidateTransitionStep
    ├── [2] ChangeStateStep
    ├── [3] RunOnExecuteTasksStep
    │   ├── Task: validate-document
    │   └── Task: send-notification
    ├── [4] RunAutomaticTransitionsStep
    ├── [5] HandleSubFlowStep
    ├── [6] HandleFinishStep
    └── [7] SaveInstanceStep
```

### Viewing Traces

Use observability tools to view traces:
- **Jaeger UI**: `http://localhost:16686`
- **Grafana Tempo**: Navigate to Explore → Tempo
- **Azure Application Insights**: Application Map / Transaction Search

## Aether SDK Reference

For detailed telemetry configuration, refer to the Aether SDK documentation:

- **`AddAetherTelemetry()`**: Configures OpenTelemetry tracing, logging, and metrics
- **`[Trace]` Aspect**: Automatic span creation for methods
- **`[Log]` Aspect**: Automatic logging of method entry/exit

## References

- [OpenTelemetry Logging Specification](https://opentelemetry.io/docs/specs/otel/logs/)
- [OpenTelemetry Tracing Specification](https://opentelemetry.io/docs/specs/otel/trace/)
- [.NET LoggerMessage Source Generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
- [System.Diagnostics.Activity](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity)
