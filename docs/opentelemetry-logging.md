# OpenTelemetry Logging Architecture

## Overview

vNext workflow engine uses OpenTelemetry for distributed tracing and structured logging. This document describes the logging architecture, conventions, and usage patterns.

## Architecture Components

### 1. VNextLogEnricherProcessor

A custom `BaseProcessor<LogRecord>` that automatically enriches all logs with:
- **class**: Extracted from logger category name
- **prefix**: Determined by namespace (vnext.exec, vnext.orch, vnext.app, vnext.infra, vnext.domain)
- **event.id**: EventId number from LoggerMessage attribute
- **event.name**: EventId name from LoggerMessage attribute
- **traceId/spanId**: Automatically correlated with active OpenTelemetry spans
- **http.header.***: Configured HTTP headers from incoming requests
- **custom attributes**: User-defined attributes from configuration

Features:
- Filters logs based on configured `ExcludedPaths` (regex patterns)
- Enriches with HTTP request headers (e.g., x-correlation-id, x-request-id)
- Adds custom attributes defined in configuration

Located: `BBT.Workflow.HttpApi.Shared/Telemetry/VNextLogEnricherProcessor.cs`

### 2. LogScope Helper

Provides extension methods to create structured logging scopes with automatic metadata enrichment:
- `ForTransition()`: For transition execution contexts
- `ForInstance()`: For instance operations
- `ForTask()`: For task execution
- `ForJob()`: For background jobs

Uses `CallerMemberName` and `CallerFilePath` attributes to automatically capture method and class names.

Located: `BBT.Workflow.Application/Telemetry/LogScope.cs`

**Implementation Status**: ✅ **Fully Integrated**
- TransitionPipeline uses `ForTransition()` for all transition executions
- Background job handlers (TransitionJobHandler, TransitionTimerJobHandler, FlowTimeoutJobHandler) use `ForJob()`
- All logs within these scopes automatically include structured metadata (domain, flow, flowVersion, instanceId, transitionKey, jobName, jobId)

### 3. Source-Generated LoggerMessage

High-performance, zero-allocation logging methods using C# source generators:
- **WorkflowLogs**: Transition, pipeline, task, instance, and job logs
- **InfrastructureLogs**: Database, cache, HTTP, and ClickHouse logs

Located: 
- `BBT.Workflow.Application/Telemetry/WorkflowLogs.cs`
- `BBT.Workflow.HttpApi.Shared/Telemetry/InfrastructureLogs.cs`

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

Located: `BBT.Workflow.Application/Telemetry/WorkflowEventIds.cs`

### 4.1. TelemetryConstants

Centralized constants for telemetry prefixes and scope field names:
- **Prefixes**: `vnext.exec`, `vnext.app`, `vnext.orch`, `vnext.infra`, `vnext.domain`
- **ScopeFields**: `domain`, `flow`, `flowVersion`, `instanceId`, `transitionKey`, `taskKey`, `taskType`, `jobName`, `jobId`, `method`, `class`

Located: `BBT.Workflow.Application/Telemetry/TelemetryConstants.cs`

### 5. TraceContextMiddleware

Middleware that automatically adds OpenTelemetry trace context to HTTP response headers:
- **X-Trace-Id**: W3C trace ID for locating traces in observability tools
- **X-Span-Id**: Current span ID for this specific request
- **traceparent**: W3C standard trace context header
- **X-Trace-State**: Optional trace state propagation

Automatically registered in `UseWorkflowApiBase()` pipeline, no manual configuration needed.

Located: `BBT.Workflow.HttpApi.Shared/Middlewares/TraceContextMiddleware.cs`

## Configuration

### appsettings.json

```json
{
  "Telemetry": {
    "ServiceName": "vnext-execution",
    "ServiceVersion": "1.0.0",
    "Otlp": {
      "Endpoint": "http://localhost:4318",
      "Protocol": "http"
    },
    "Tracing": {
      "FilterDaprInternalOperations": true,
      "ExcludedUrlPatterns": [
        "/dapr.proto.runtime",
        "/v1.0/lock",
        "/v1.0/unlock"
      ]
    },
    "Logging": {
      "Enabled": true,
      "IncludeFormattedMessage": true,
      "IncludeScopes": true,
      "ParseStateValues": true,
      "EnableConsoleExporter": true,
      "EnableOtlpExporter": true,
      "ExcludedPaths": [
        "^/swagger(?:/.*)?$",
        "^/dapr(?:/.*)?$",
        "^/health$",
        "^/healthz$",
        "^/v1/metrics$",
        "^/metrics$",
        "^/v1/traces",
        "^/traces",
        "^/$"
      ],
      "Enrichers": {
        "Headers": [
          "x-correlation-id",
          "x-request-id",
          "x-tenant-id"
        ],
        "CustomAttributes": {
          "environment": "development",
          "team": "workflow-team"
        }
      }
    }
  }
}
```

#### Configuration Options

##### Environment Detection

The environment is automatically detected from **`ASPNETCORE_ENVIRONMENT`** environment variable. You don't need to configure it in `appsettings.json`.

**How it works:**
1. Reads `ASPNETCORE_ENVIRONMENT` from environment variables
2. Falls back to `"Production"` if not set
3. Used for:
   - Resource attributes: `deployment.environment`
   - Conditional console exporters (enabled in Development)
   - OpenTelemetry service metadata

**Setting the environment:**

- **Development (launchSettings.json)**:
  ```json
  {
    "profiles": {
      "http": {
        "environmentVariables": {
          "ASPNETCORE_ENVIRONMENT": "Development"
        }
      }
    }
  }
  ```

- **Docker / Docker Compose**:
  ```yaml
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
  ```

- **Kubernetes**:
  ```yaml
  env:
    - name: ASPNETCORE_ENVIRONMENT
      value: "Production"
  ```

- **Command Line**:
  ```bash
  export ASPNETCORE_ENVIRONMENT=Staging
  dotnet run
  ```

**Benefits:**
- ✅ Standard .NET convention
- ✅ Single source of truth
- ✅ Works consistently across all deployment methods
- ✅ No duplication in configuration

##### Telemetry.Tracing.FilterDaprInternalOperations

Controls whether Dapr internal operations (lock, unlock, etc.) are filtered from traces to reduce noise.

- **`true`** (default): Filters out Dapr internal operations
- **`false`**: Includes all Dapr operations in traces

**What gets filtered:**
- Dapr gRPC operations: `/dapr.proto.runtime.v1.Dapr/*`
- Dapr lock operations: `/v1.0/lock`
- Dapr unlock operations: `/v1.0/unlock`
- OTLP exporter requests (always filtered)
- Health check requests (always filtered)

**Why filter Dapr operations?**
- ❌ Without filtering: Traces cluttered with lock/unlock spans that have no span names
- ✅ With filtering: Clean traces showing only business operations
- 🎯 Focus on what matters: workflow transitions, tasks, and business logic

**When to disable:**
- Debugging Dapr lock contention issues
- Investigating distributed lock behavior
- Performance analysis of Dapr sidecar

##### Telemetry.Tracing.ExcludedUrlPatterns

List of URL patterns to exclude from tracing. Supports "contains" matching (case-insensitive).

Default patterns:
```json
{
  "Tracing": {
    "ExcludedUrlPatterns": [
      "/dapr.proto.runtime",
      "/v1.0/lock",
      "/v1.0/unlock"
    ]
  }
}
```

You can add more patterns:
```json
{
  "Tracing": {
    "ExcludedUrlPatterns": [
      "/dapr.proto.runtime",
      "/v1.0/lock",
      "/v1.0/unlock",
      "/internal/",
      "/metrics",
      "/actuator"
    ]
  }
}
```

**How it works:**
- Each HttpClient request URI is checked against patterns
- If URI contains any pattern (case-insensitive), span is not created
- Reduces trace noise and improves observability signal-to-noise ratio

##### Telemetry.Logging.Enabled
Controls whether OpenTelemetry logging integration is enabled.

- **`true`** (default): Enables OpenTelemetry logging with all configured exporters and enrichers
- **`false`**: Disables OpenTelemetry logging integration completely. Standard .NET logging will still work with configured providers (Console, Debug, etc.)

When disabled:
- No logs are sent to OTLP endpoint
- No log enrichment with trace context
- No custom attributes or headers are added
- Standard .NET logging providers continue to work normally
- Performance impact is minimal as the entire OpenTelemetry logging pipeline is skipped

Example to disable OpenTelemetry logging while keeping standard .NET logging:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Telemetry": {
    "Logging": {
      "Enabled": false
    }
  }
}
```

##### Log Levels (Standard .NET Configuration)

Log levels are configured using the standard .NET `Logging:LogLevel` section in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "System.Net.Http.HttpClient": "Warning",
      "BBT.Workflow": "Debug"
    }
  }
}
```

Available log levels:
- `Trace`: Most verbose, all logs
- `Debug`: Detailed flow information
- `Information`: Standard operational logs (recommended default)
- `Warning`: Warning messages only
- `Error`: Error messages only
- `Critical`: Critical errors only
- `None`: Disable logging for that category

You can configure different log levels for different namespaces:
- `Default`: Base level for all categories
- `Microsoft.*`: Framework logs
- `BBT.Workflow.*`: Application logs
- `BBT.Workflow.Execution.*`: Execution layer logs only

##### Telemetry.Logging.ExcludedPaths
List of regex patterns to exclude specific HTTP request paths from logging and tracing. This helps reduce noise from:
- Health check endpoints (`/health`, `/healthz`)
- Metrics endpoints (`/metrics`)
- Swagger UI (`/swagger`)
- Dapr endpoints (`/dapr`)

Patterns support full regex syntax:
- `^/health$` - Exact match
- `^/swagger(?:/.*)?$` - Match swagger and all sub-paths
- `^/api/v[0-9]+/health$` - Pattern matching

**Note**: This is different from the standard .NET `Logging:LogLevel` configuration. `ExcludedPaths` filters logs based on HTTP request paths, while `LogLevel` filters based on log severity and category.

##### Telemetry.Logging.Enrichers.Headers
List of HTTP header names to capture and add to log attributes. Headers are prefixed with `http.header.` in the log output.

Common headers to capture:
- `x-correlation-id`: Request correlation across services
- `x-request-id`: Unique request identifier
- `x-tenant-id`: Multi-tenant identifier
- `x-user-id`: User identifier (be careful with PII)

##### Telemetry.Logging.Enrichers.CustomAttributes
Dictionary of custom key-value pairs to add to all logs. Useful for:
- Environment markers
- Team ownership
- Application version
- Deployment region
- Any static metadata

### Program.cs Registration

The telemetry is automatically configured in `WorkflowApiBaseServiceCollectionExtensions`:

```csharp
// Simple registration (environment auto-detected from ASPNETCORE_ENVIRONMENT)
services.AddVNextTelemetry(configuration);

// Or with explicit environment (optional)
services.AddVNextTelemetry(configuration, builder.Environment);
```

This replaces the previous Aether framework telemetry configuration.

**Environment Resolution:**
1. If `IHostEnvironment` is passed → uses `environment.EnvironmentName`
2. Otherwise → reads `ASPNETCORE_ENVIRONMENT` environment variable
3. Falls back to `"Production"` if not set

### Trace Context in Response Headers

A middleware automatically adds OpenTelemetry trace context to all HTTP response headers. This is configured in `UseWorkflowApiBase()` and runs automatically for all workflow APIs.

The following headers are added to every response:
- **X-Trace-Id**: W3C trace ID (32 hex characters) - Use this to find the trace in your observability tool
- **X-Span-Id**: W3C span ID (16 hex characters) - The specific span for this request
- **traceparent**: W3C standard trace context header (format: `00-traceId-spanId-flags`)
- **X-Trace-State**: Optional trace state information (if present)

Example response headers:
```http
HTTP/1.1 200 OK
X-Trace-Id: 4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a
X-Span-Id: 1a2b3c4d5e6f7890
traceparent: 00-4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a-1a2b3c4d5e6f7890-01
```

**Usage**: Copy the `X-Trace-Id` value from the response and paste it into your trace viewer (Jaeger, Zipkin, Grafana Tempo) to see the full distributed trace for that request.

## Usage Patterns

### 1. Transition Execution Logging

```csharp
using var scope = _logger.ForTransition(
    context.Workflow.Key,
    context.Workflow.Key,
    context.Workflow.Version?.ToString(),
    context.InstanceId,
    context.TransitionKey);

_logger.TransitionStarted(
    TelemetryConstants.Prefixes.Execution,
    context.TransitionKey,
    context.InstanceId,
    context.WorkflowKey);

// ... execution logic ...

_logger.TransitionCompleted(
    TelemetryConstants.Prefixes.Execution,
    context.TransitionKey,
    context.InstanceId,
    elapsedMs);
```

**Using TelemetryConstants**:
- `TelemetryConstants.Prefixes.Execution` → `"vnext.exec"`
- `TelemetryConstants.Prefixes.Application` → `"vnext.app"`
- `TelemetryConstants.Prefixes.Orchestration` → `"vnext.orch"`
- `TelemetryConstants.Prefixes.Infrastructure` → `"vnext.infra"`
- `TelemetryConstants.Prefixes.Domain` → `"vnext.domain"`

### 2. Task Execution Logging

```csharp
using var scope = _logger.ForTask(
    workflow.Key,
    workflow.Key,
    workflow.Version?.ToString(),
    instanceId,
    taskKey,
    taskType);

_logger.TaskExecutionStarted(
    TelemetryConstants.Prefixes.Execution,
    taskKey,
    taskType,
    instanceId);

// ... task execution ...

_logger.TaskExecutionCompleted(
    TelemetryConstants.Prefixes.Execution,
    taskKey,
    taskType,
    instanceId,
    elapsedMs);
```

### 3. Pipeline Step Logging

```csharp
_logger.PipelineStepStarted(TelemetryConstants.Prefixes.Execution, stepName, context.InstanceId);

// ... step execution ...

_logger.PipelineStepCompleted(TelemetryConstants.Prefixes.Execution, stepName, context.InstanceId, elapsedMs);
```

### 4. Error Logging

```csharp
try
{
    // ... execution logic ...
}
catch (TransitionRuleFailedException ex)
{
    _logger.TransitionRuleFailed(
        TelemetryConstants.Prefixes.Execution,
        context.TransitionKey,
        context.InstanceId,
        ex.Message);
    throw;
}
catch (Exception ex)
{
    _logger.TransitionFailed(ex, TelemetryConstants.Prefixes.Execution, context.TransitionKey, context.InstanceId);
    throw;
}
```

## Log Schema

All logs follow a consistent schema:

```json
{
  "ts": "2025-10-11T10:30:45.123Z",
  "level": "Information",
  "message": "vnext.exec Transition auto-approve started for instance 8b3e...",
  "event.id": 10001,
  "event.name": "TransitionStarted",
  "category": "BBT.Workflow.Execution.Pipeline.TransitionPipeline",
  "prefix": "vnext.exec",
  "class": "TransitionPipeline",
  "method": "RunAsync",
  "domain": "amorphie",
  "flow": "loan-approval",
  "instanceId": "8b3e4a5c-...",
  "transitionKey": "auto-approve",
  "http.header.x-correlation-id": "abc123...",
  "http.header.x-request-id": "def456...",
  "environment": "development",
  "team": "workflow-team",
  "traceId": "4d5e6f...",
  "spanId": "1a2b3c..."
}
```

## Filtering and Querying

### Filter by Prefix

To see only execution logs:
```
prefix:"vnext.exec"
```

To see only orchestration logs:
```
prefix:"vnext.orch"
```

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
NOT category:Microsoft.* AND NOT category:System.* AND prefix:vnext.*
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
// Good
_logger.TransitionStarted("vnext.exec", key, id, workflow);

// Avoid
_logger.LogInformation("Transition {Key} started", key);
```

### 3. Include Timing Information

Always measure and log execution time for significant operations:
```csharp
var sw = Stopwatch.StartNew();
// ... operation ...
sw.Stop();
_logger.OperationCompleted(..., sw.ElapsedMilliseconds);
```

### 4. Consistent Prefix Usage

Use the correct prefix for your layer:
- `vnext.exec`: Execution layer
- `vnext.orch`: Orchestration layer
- `vnext.app`: Application layer
- `vnext.infra`: Infrastructure layer
- `vnext.domain`: Domain layer

### 5. Appropriate Log Levels

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
- ✅ Enable `FilterDaprInternalOperations: true` (default)
- ✅ Add internal endpoints to `ExcludedUrlPatterns`
- ✅ Filter health checks, metrics endpoints
- ❌ Don't include infrastructure noise in production traces
- 🎯 Focus: Business transactions and user journeys

Example for clean production traces:
```json
{
  "Telemetry": {
    "Tracing": {
      "FilterDaprInternalOperations": true,
      "ExcludedUrlPatterns": [
        "/dapr.proto.runtime",
        "/v1.0/lock",
        "/v1.0/unlock",
        "/internal/",
        "/actuator"
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
      "FilterDaprInternalOperations": false
    }
  }
}
```

When disabled, Dapr operations will have improved span names:
- `Dapr.LockAlpha1` instead of empty
- `Dapr.UnlockAlpha1` instead of empty
- `Dapr.Lock` for HTTP API
- `Dapr.Unlock` for HTTP API

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
```

### After
```csharp
using var scope = _logger.ForTransition(domain, flow, instanceId, key);
_logger.TransitionStarted("vnext.exec", key, instanceId, workflowKey);
```

Benefits:
- Structured, queryable fields
- Automatic enrichment with class/method/prefix
- Trace correlation
- Zero-allocation performance
- Consistent schema across the system

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

## Distributed Tracing with Custom Spans

### WorkflowActivitySource

The workflow engine uses a custom `ActivitySource` to create distributed tracing spans for key workflow operations.

Located: `BBT.Workflow.Application/Telemetry/WorkflowActivitySource.cs`

```csharp
public static class WorkflowActivitySource
{
    public static ActivitySource Instance { get; } = new(
        TelemetryConstants.ActivitySourceName,    // "BBT.Workflow"
        TelemetryConstants.ActivitySourceVersion); // "1.0.0"
}
```

### Tracing Architecture

The workflow engine creates hierarchical spans to represent the execution flow:

```
HTTP Request (ASP.NET Core)
└── Transition Execution (SyncTransitionStrategy)
    ├── Handler PreHandle
    ├── Pipeline Execution (TransitionPipeline)
    │   ├── Pipeline Step [1] ValidateTransitionStep
    │   ├── Pipeline Step [2] ChangeStateStep
    │   ├── Pipeline Step [3] RunOnExecuteTasksStep
    │   │   ├── Task Execution: TaskA
    │   │   └── Task Execution: TaskB
    │   └── Pipeline Step [4] HandleSubFlowStep
    │       └── SubFlow Start: ChildFlow
    └── Handler PostHandle
```

### Span Types

#### 1. Transition Execution Span
Created in `SyncTransitionStrategy` to represent the entire transition execution lifecycle.

**Span Name**: `transition.execute`  
**Display Name**: `{instanceId}/{transitionKey}`

**Tags**:
- `workflow.domain`: Domain name
- `workflow.flow`: Flow key
- `workflow.flow.version`: Flow version
- `workflow.instance.id`: Instance ID
- `workflow.transition.key`: Transition key
- `workflow.trigger.type`: Trigger type (Manual, Event, Timer, etc.)
- `workflow.handler.name`: Handler class name

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Strategy/SyncTransitionStrategy.cs`

#### 2. Handler Spans
Created in `TransitionHandlerBase` for PreHandle and PostHandle operations.

**Span Names**: 
- `handler.prehandle` - Pre-processing logic
- `handler.posthandle` - Post-processing logic

**Tags**:
- `workflow.handler.name`: Handler class name
- `workflow.trigger.type`: Trigger type

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Handlers/TransitionHandlerBase.cs`

#### 3. Pipeline Execution Span
Created in `TransitionPipeline` to represent the entire pipeline execution.

**Span Name**: `pipeline.execute`

Contains child spans for each pipeline step.

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Pipeline/TransitionPipeline.cs`

#### 4. Pipeline Step Spans
Created for each pipeline step execution.

**Span Name**: `pipeline.step`  
**Display Name**: `[{stepOrder}] {stepName}`

**Tags**:
- `workflow.step.name`: Step class name
- `workflow.step.order`: Step execution order

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Pipeline/TransitionPipeline.cs`

#### 5. Task Execution Span
Created in `LocalTaskExecutor` for task execution.

**Span Name**: `task.execute`  
**Display Name**: `Task: {taskKey}`

**Tags**:
- `workflow.task.key`: Task key
- `workflow.task.type`: Task type (Script, DaprService, DaprBinding, etc.)
- `workflow.instance.id`: Instance ID

**Location**: `src/BBT.Workflow.Application/Tasks/Execution/LocalTaskExecutor.cs`

#### 6. SubFlow Spans
Created in `SubflowStarter` and `SubflowCompletionService` for SubFlow lifecycle.

**Span Names**:
- `subflow.start` - SubFlow initiation
- `subflow.complete` - SubFlow completion

**Display Names**:
- `SubFlow Start: {subFlowKey}`
- `SubFlow Complete: {subFlowKey}`

**Tags**:
- `workflow.subflow.key`: SubFlow key
- `workflow.domain`: Domain name
- `workflow.instance.id`: Parent instance ID

**Locations**:
- `src/BBT.Workflow.Application/SubFlow/Services/SubflowStarter.cs`
- `src/BBT.Workflow.Application/SubFlow/Services/SubflowCompletionService.cs`

### Span Events

Events are lightweight point-in-time markers within spans that capture important moments during workflow execution.

#### 1. State Change Event
Recorded in `ChangeStateStep` when a state transition occurs.

**Event Name**: `state.changed`

**Tags**:
- `workflow.state.from`: Previous state
- `workflow.state.to`: New state

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Pipeline/Steps/ChangeStateStep.cs`

#### 2. SubFlow Initiated Event
Recorded in `HandleSubFlowStep` when a SubFlow/SubProcess is successfully started.

**Event Name**: `subflow.initiated`

**Tags**:
- `workflow.subflow.key`: SubFlow process key
- `workflow.domain`: SubFlow domain
- `workflow.subflow.type`: Type code ("S" for SubFlow, "P" for SubProcess)
- `workflow.subflow.version`: SubFlow version
- `workflow.correlation.id`: Correlation tracking ID
- `workflow.subflow.instance.id`: Created SubFlow instance ID

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Pipeline/Steps/HandleSubFlowStep.cs`

#### 3. Workflow Completed Event
Recorded in `HandleFinishStep` when a workflow instance reaches a Finish state.

**Event Name**: `workflow.completed`

**Tags**:
- `workflow.instance.id`: Completed instance ID
- `workflow.flow`: Flow key
- `workflow.domain`: Domain name
- `workflow.completed.state`: Final state key
- `workflow.is.subflow`: Whether this is a SubFlow instance
- `workflow.duration.ms`: Total workflow duration in milliseconds

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Pipeline/Steps/HandleFinishStep.cs`

#### 4. Completion Event Published
Recorded in `HandleFinishStep` when flow completion event is successfully published to Dapr pub/sub.

**Event Name**: `completion.event.published`

**Tags**:
- `workflow.instance.id`: Instance ID
- `workflow.flow`: Flow key
- `workflow.domain`: Domain name
- `pubsub.store`: Dapr pub/sub store name
- `pubsub.topic`: Published topic name
- `workflow.completed.state`: Completed state
- `workflow.duration.ms`: Workflow duration

**Location**: `src/BBT.Workflow.Application/Execution/Transitions/Pipeline/Steps/HandleFinishStep.cs`

### ActivityExtensions Helper

Provides extension methods to simplify span operations:

**Located**: `src/BBT.Workflow.Application/Telemetry/ActivityExtensions.cs`

```csharp
// Set display name
activity?.SetDisplayName("Custom Name");

// Record exception and set error status
activity?.RecordExceptionWithStatus(exception, "Optional description");
```

### Registering Custom ActivitySource

The `WorkflowActivitySource` is automatically registered in the OpenTelemetry tracing configuration:

**Location**: `src/BBT.Workflow.HttpApi.Shared/Microsoft/Extensions/DependencyInjection/VNextTelemetryServiceCollectionExtensions.cs`

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(TelemetryConstants.ActivitySourceName) // "BBT.Workflow"
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });
```

### Span Hierarchy Example

Typical trace for a transition execution with tasks and SubFlow:

```
00-a1b2c3d4e5f6... (TraceId)
├── POST /api/v1/execution/instances/{id}/transitions/{key}
│   ├── transition.execute: "abc-123/submit-application"
│   │   ├── handler.prehandle
│   │   ├── pipeline.execute
│   │   │   ├── [1] ValidateTransitionStep
│   │   │   ├── [2] ChangeStateStep
│   │   │   │   └── ✓ Event: state.changed (Draft → Submitted)
│   │   │   ├── [3] RunOnExecuteTasksStep
│   │   │   │   ├── task.execute: "validate-document"
│   │   │   │   ├── task.execute: "send-notification"
│   │   │   │   └── task.execute: "update-external-system"
│   │   │   ├── [4] HandleSubFlowStep
│   │   │   │   ├── subflow.start: "approval-workflow"
│   │   │   │   └── ✓ Event: subflow.initiated (type: S, correlation: xyz-789)
│   │   │   ├── [5] HandleFinishStep
│   │   │   │   ├── ✓ Event: workflow.completed (duration: 1250ms)
│   │   │   │   └── ✓ Event: completion.event.published (topic: flow.completed)
│   │   │   └── [6] SaveInstanceStep
│   │   └── handler.posthandle
```

### Viewing Traces

Traces can be viewed in observability tools:

1. **Jaeger UI**: `http://localhost:16686`
2. **Grafana Tempo**: Navigate to Explore → Tempo
3. **Application Insights**: Azure Portal → Application Map / Transaction Search

Use the `X-Trace-Id` from response headers to find specific traces:

```bash
curl -i http://localhost:5001/api/v1/execution/instances/{id}/transitions/{key}

# Response:
# X-Trace-Id: a1b2c3d4e5f6789...
# X-Span-Id: 1a2b3c4d5e6f...
```

## References

- [OpenTelemetry Logging Specification](https://opentelemetry.io/docs/specs/otel/logs/)
- [OpenTelemetry Tracing Specification](https://opentelemetry.io/docs/specs/otel/trace/)
- [.NET LoggerMessage Source Generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
- [Structured Logging Best Practices](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/)
- [System.Diagnostics.Activity](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity)

