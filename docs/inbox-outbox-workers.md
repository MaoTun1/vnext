# Inbox/Outbox Workers Architecture

## Overview

The BBT Workflow Engine implements the **Transactional Outbox** and **Inbox** patterns through dedicated worker services. These patterns ensure reliable message delivery and event processing in distributed systems by decoupling event publishing from business transactions.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Event Flow Architecture                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────────────────┐ │
│  │ Orchestration│     │   Database   │     │      Dapr PubSub         │ │
│  │     API      │────►│    Outbox    │────►│    (Message Broker)      │ │
│  └──────────────┘     │    Table     │     └──────────────────────────┘ │
│         │             └──────────────┘                │                  │
│         │                    ▲                        │                  │
│         │                    │                        ▼                  │
│         │             ┌──────────────┐     ┌──────────────────────────┐ │
│         │             │    Outbox    │     │      Inbox Worker        │ │
│         │             │    Worker    │     │   (Event Processing)     │ │
│         │             └──────────────┘     └──────────────────────────┘ │
│         │                                             │                  │
│         │                                             ▼                  │
│         │                                  ┌──────────────────────────┐ │
│         └─────────────────────────────────►│      Database Inbox      │ │
│                                            │         Table            │ │
│                                            └──────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

## Worker Projects

### 1. Inbox Worker (`BBT.Workflow.Workers.Inbox`)

Processes incoming events from the message broker and handles domain event consumption.

**Location:** `workers/BBT.Workflow.Workers.Inbox/`

#### Program.cs

```csharp
using BBT.Aether.AspNetCore.Dapr;
using BBT.Aether.AspNetCore.Threads;
using Dapr.Client;
using Dapr.Extensions.Configuration;

ThreadPoolHelper.ConfigureThreadPool();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());

// Dapr Optional - Secret Store integration
if(builder.Configuration.GetValue<bool>("Vault:Enabled", false))
{
    var daprClient = new DaprClientBuilder().Build();
    await DaprCheckForSidecarHelper.CheckAsync(daprClient);
    builder.Configuration.AddDaprSecretStore(
        builder.Configuration["DAPR_SECRET_STORE_NAME"] ?? "vnext-secret", 
        daprClient);
}

builder.Services.AddWorkerInboxModule();

var host = builder.Build();
host.UseWorkerInbox();
await host.RunAsync();
```

#### Service Registration

```csharp
public static IServiceCollection AddWorkerInboxModule(this IServiceCollection services)
{
    var configuration = services.GetConfiguration();
    services
        .AddDomainModule()
        .AddApplicationModule()
        .AddInfrastructureModule()
        .AddAspNetCoreModules(configuration)
        .AddResultResilience(configuration)
        .AddDaprClients()
        .AddEventBus(configuration)
        .AddDbContext(configuration)
        .AppMapper()
        .AddTelemetry(configuration)
        .AddDistributedCache(configuration)
        .AddDistributedLock(configuration)
        .AddBackgroundJob()
        .AddRedis()
        .AddExceptionHandling()
        .AddRuntimeMiddleware()
        .AddHeaderService()
        .AddWorkflowHttpClient()
        .AddHostedServices()
        .AddAppHealthChecks();
    return services;
}

private static IServiceCollection AddHostedServices(this IServiceCollection services)
{
    services.AddHostedService<InboxProcessorHostedService>();
    return services;
}
```

#### InboxProcessorHostedService

```csharp
public sealed class InboxProcessorHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<InboxProcessorHostedService> logger,
    AetherOutboxOptions options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Inbox Processor Worker starting. Processing interval: {Interval}",
            options.ProcessingInterval);

        // Wait a bit before starting to allow other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IInboxProcessor>();
                await processor.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Inbox cancelled");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during inbox cycle");
            }

            try
            {
                await Task.Delay(options.ProcessingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Inbox Processor Worker stopped");
    }
}
```

### 2. Outbox Worker (`BBT.Workflow.Workers.Outbox`)

Publishes pending events from the outbox table to the message broker.

**Location:** `workers/BBT.Workflow.Workers.Outbox/`

#### OutboxProcessorHostedService

```csharp
public sealed class OutboxProcessorHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessorHostedService> logger,
    AetherOutboxOptions options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Outbox Processor Worker starting. Processing interval: {Interval}",
            options.ProcessingInterval);

        // Wait a bit before starting to allow other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await processor.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Outbox processing cancelled");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during outbox processing cycle");
            }

            try
            {
                await Task.Delay(options.ProcessingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Outbox Processor Worker stopped");
    }
}
```

## Event Handlers

### InstanceSubCompletedEventHandler

Handles subflow completion events to resume parent workflows:

```csharp
internal sealed class InstanceSubCompletedEventHandler(
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<InstanceSubCompletedEventHandler> logger) : IEventHandler<InstanceSubCompletedEvent>
{
    public async Task HandleAsync(
        CloudEventEnvelope<InstanceSubCompletedEvent> envelope, 
        CancellationToken cancellationToken)
    {
        var eventData = envelope.Data;

        // Domain filtering - only process events for this runtime's domain
        if (!runtimeInfoProvider.IsDomainMatch(eventData.Domain))
        {
            logger.SubFlowEventIgnoredDomainMismatch(
                eventData.Domain,
                runtimeInfoProvider.Domain,
                eventData.SubInstanceId,
                eventData.InstanceId);
            return;
        }
        
        logger.SubFlowEventReceived(
            eventData.SubInstanceId,
            eventData.InstanceId,
            eventData.Domain,
            eventData.Flow);

        // Switch to appropriate schema context
        using (currentSchema.Use(eventData.Flow))
        {
            var completedData = new FlowCompletedInput
            {
                SubInstanceId = eventData.SubInstanceId,
                InstanceId = eventData.InstanceId,
                Domain = eventData.Domain,
                Flow = eventData.Flow,
                Version = eventData.Version,
                CompletedState = eventData.CompletedState,
                InstanceData = eventData.InstanceData,
                CompletedAt = eventData.CompletedAt,
                Duration = eventData.Duration
            };

            await using var scope = scopeFactory.CreateAsyncScope();
            var subflowCompletionService = scope.ServiceProvider
                .GetRequiredService<ISubflowCompletionService>();
            await subflowCompletionService.CompletionAsync(completedData, cancellationToken);
        }
    }
}
```

### InstanceCanceledEventHandler

Handles instance cancellation events for cleanup:

```csharp
internal sealed class InstanceCanceledEventHandler(
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<InstanceCanceledEventHandler> logger) : IEventHandler<InstanceCanceledEvent>
{
    public async Task HandleAsync(
        CloudEventEnvelope<InstanceCanceledEvent> envelope,
        CancellationToken cancellationToken)
    {
        var eventData = envelope.Data;

        // Domain filtering
        if (!runtimeInfoProvider.IsDomainMatch(eventData.Domain))
        {
            logger.InstanceCanceledEventIgnoredDomainMismatch(
                eventData.Domain,
                runtimeInfoProvider.Domain,
                eventData.InstanceId,
                eventData.Flow);
            return;
        }

        logger.InstanceCanceledEventReceived(
            eventData.InstanceId,
            eventData.Flow);

        using (currentSchema.Use(eventData.Flow))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cancellationService = scope.ServiceProvider
                .GetRequiredService<IInstanceCancellationService>();
            var result = await cancellationService.ProcessCancellationAsync(
                eventData.InstanceId,
                cancellationToken);

            if (!result.IsSuccess)
            {
                logger.InstanceCanceledProcessingFailed(
                    new InvalidOperationException(result.Error.Message),
                    eventData.InstanceId);
            }
        }
    }
}
```

### ChildSubflowCancelRequestedEventHandler

Handles cancellation requests for child subflows:

```csharp
internal sealed class ChildSubflowCancelRequestedEventHandler(
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<ChildSubflowCancelRequestedEventHandler> logger) 
    : IEventHandler<ChildSubflowCancelRequestedEvent>
{
    public async Task HandleAsync(
        CloudEventEnvelope<ChildSubflowCancelRequestedEvent> envelope,
        CancellationToken cancellationToken)
    {
        // Similar pattern: domain check, schema switch, service delegation
    }
}
```

## Aether SDK Integration

### IInboxProcessor

The `IInboxProcessor` from Aether SDK handles inbox message processing:

- Reads pending messages from the inbox table
- Deserializes events to their typed representations
- Routes events to registered handlers
- Manages message acknowledgment and retry

### IOutboxProcessor

The `IOutboxProcessor` from Aether SDK handles outbox message publishing:

- Reads pending messages from the outbox table
- Publishes events to Dapr PubSub
- Marks messages as processed
- Handles retry logic for failed publishes

### Configuration

```json
{
  "AetherOutbox": {
    "ProcessingInterval": "00:00:05",
    "BatchSize": 100,
    "MaxRetryCount": 3,
    "RetryDelaySeconds": 30
  }
}
```

## Transactional Outbox Pattern

### How It Works

1. **Business Transaction**: When a domain event occurs, it's written to the outbox table within the same database transaction as the business data
2. **Eventual Delivery**: The Outbox Worker polls the outbox table and publishes events to the message broker
3. **At-Least-Once Delivery**: Events are guaranteed to be published at least once
4. **Idempotent Handlers**: Inbox handlers must be idempotent to handle duplicate deliveries

```
┌─────────────────────────────────────────────────────────────────┐
│  Business Transaction                                            │
│  ┌───────────────────┐  ┌───────────────────┐                   │
│  │  Update Entity    │  │  Write to Outbox  │   SAME TX         │
│  │                   │  │                   │   ────────         │
│  │  instance.Complete│  │  AddDistributedEvent│                  │
│  └───────────────────┘  └───────────────────┘                   │
│            │                      │                              │
│            └──────────────────────┘                              │
│                      │                                           │
│                      ▼                                           │
│            ┌─────────────────┐                                  │
│            │     COMMIT      │                                  │
│            └─────────────────┘                                  │
└─────────────────────────────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  Outbox Worker (Async)                                          │
│  ┌───────────────────┐  ┌───────────────────┐                   │
│  │  Read from Outbox │  │  Publish to Dapr  │                   │
│  │                   │──│     PubSub        │                   │
│  └───────────────────┘  └───────────────────┘                   │
│            │                      │                              │
│            └──────────────────────┘                              │
│                      │                                           │
│                      ▼                                           │
│            ┌─────────────────┐                                  │
│            │  Mark Processed │                                  │
│            └─────────────────┘                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Deployment

### Docker Configuration

Each worker has its own Dockerfile:

```dockerfile
# Inbox Worker Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["workers/BBT.Workflow.Workers.Inbox/BBT.Workflow.Workers.Inbox.csproj", "workers/BBT.Workflow.Workers.Inbox/"]
RUN dotnet restore "workers/BBT.Workflow.Workers.Inbox/BBT.Workflow.Workers.Inbox.csproj"
COPY . .
WORKDIR "/src/workers/BBT.Workflow.Workers.Inbox"
RUN dotnet build "BBT.Workflow.Workers.Inbox.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BBT.Workflow.Workers.Inbox.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BBT.Workflow.Workers.Inbox.dll"]
```

### Dapr Configuration

Workers use Dapr for PubSub integration. Example component:

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
    - name: redisHost
      value: redis:6379
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: vnext-inbox-worker
spec:
  replicas: 2
  selector:
    matchLabels:
      app: vnext-inbox-worker
  template:
    metadata:
      labels:
        app: vnext-inbox-worker
      annotations:
        dapr.io/enabled: "true"
        dapr.io/app-id: "vnext-inbox-worker"
        dapr.io/app-port: "80"
    spec:
      containers:
        - name: inbox-worker
          image: vnext-inbox-worker:latest
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
```

## Multi-Schema Support

Workers respect the multi-schema architecture:

```csharp
// Switch to appropriate schema before processing
using (currentSchema.Use(eventData.Flow))
{
    // All database operations use the correct schema
    await service.ProcessAsync(eventData, cancellationToken);
}
```

## Error Handling

### Retry Strategy

Workers implement retry logic for transient failures:

1. **Immediate Retry**: For network glitches
2. **Delayed Retry**: For temporary unavailability
3. **Dead Letter**: For permanent failures after max retries

### Logging

Structured logging with correlation:

```csharp
logger.SubFlowEventReceived(
    eventData.SubInstanceId,
    eventData.InstanceId,
    eventData.Domain,
    eventData.Flow);

logger.InstanceCanceledProcessingFailed(
    new InvalidOperationException(result.Error.Message),
    eventData.InstanceId);
```

## Best Practices

### 1. Idempotent Handlers

Always design handlers to be idempotent:

```csharp
public async Task HandleAsync(CloudEventEnvelope<MyEvent> envelope, CancellationToken ct)
{
    // Check if already processed
    if (await IsAlreadyProcessed(envelope.Id, ct))
        return;
    
    // Process event
    await ProcessEventAsync(envelope.Data, ct);
    
    // Mark as processed
    await MarkAsProcessed(envelope.Id, ct);
}
```

### 2. Domain Filtering

Always filter events by domain:

```csharp
if (!runtimeInfoProvider.IsDomainMatch(eventData.Domain))
{
    logger.EventIgnoredDomainMismatch(...);
    return;
}
```

### 3. Scoped Service Resolution

Create a new scope for each event:

```csharp
await using var scope = scopeFactory.CreateAsyncScope();
var service = scope.ServiceProvider.GetRequiredService<IMyService>();
```

### 4. Schema Context

Always use the correct schema context:

```csharp
using (currentSchema.Use(eventData.Flow))
{
    // All operations use correct schema
}
```

## Related Documentation

- [Event Bus Hooks](./eventbus-hooks.md) - Pre-publish hooks for events
- [Architecture Overview](./architecture-overview.md) - System architecture
- [Background Jobs](./background-jobs.md) - Dapr job scheduling

