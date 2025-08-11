# Background Jobs

## Overview

The BBT Workflow Engine provides a comprehensive background job processing system built on DAPR's job scheduling capabilities. This system handles asynchronous workflow operations including timeouts, auto-transitions, timer-based transitions, and custom job processing.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                   DAPR Runtime                          │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │  Job Scheduler  │  │  Job Storage    │             │
│  │                 │  │                 │             │
│  │ • Cron Jobs     │  │ • Job State     │             │
│  │ • One-time Jobs │  │ • Persistence   │             │
│  │ • Retries       │  │ • Clustering    │             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│              Background Job Service                     │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │ IBackgroundJob  │  │  JobDispatcher  │             │
│  │    Service      │  │                 │             │
│  │                 │  │ • Route Jobs    │             │
│  │ • Enqueue Jobs  │  │ • Handler Mgmt  │             │
│  │ • Schedule Jobs │  │ • Error Handling│             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│                 Job Handlers                            │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │ FlowTimeout     │  │ AutoTransition  │             │
│  │   Handler       │  │    Handler      │             │
│  │                 │  │                 │             │
│  │ • Timeout Logic │  │ • Auto Execution│             │
│  │ • State Changes │  │ • Batch Process │             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
```

## Core Components

### 1. IBackgroundJobService Interface

The main interface for job scheduling:

```csharp
public interface IBackgroundJobService
{
    Task EnqueueAsync<T>(
        string jobName,
        string jobId,
        DaprJobSchedule schedule,
        T payload,
        CancellationToken cancellationToken = default) where T : class;
}
```

### 2. DAPR Background Job Service Implementation

```csharp
public sealed class DaprBackgroundJobService : IBackgroundJobService
{
    private readonly ILogger<DaprBackgroundJobService> _logger;
    private readonly DaprJobsClient _daprJobsClient;
    private readonly IJobStore _jobStore;

    public async Task EnqueueAsync<T>(
        string jobName,
        string jobId,
        DaprJobSchedule schedule,
        T payload,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogInformation("Scheduling job {jobName} - {jobId}.", jobName, jobId);

        // Create job data with metadata
        var jobData = new BackgroundJobInfo<T>
        {
            JobName = jobName,
            JobId = jobId,
            ExpressionValue = schedule.ExpressionValue,
            Payload = payload,
            IsTriggered = false
        };

        // Persist job information for tracking
        await _jobStore.SaveAsync(jobId, jobData, cancellationToken);

        // Schedule with DAPR
        await _daprJobsClient.ScheduleJobAsync(
            jobName,
            schedule,
            JsonSerializer.SerializeToUtf8Bytes(jobData),
            cancellationToken: cancellationToken
        );
    }
}
```

### 3. Job Store Interface

For persisting and retrieving job information:

```csharp
public interface IJobStore
{
    Task SaveAsync<T>(string jobId, BackgroundJobInfo<T> jobInfo, CancellationToken cancellationToken = default) where T : class;
    Task<BackgroundJobInfo<T>?> GetAsync<T>(string jobId, CancellationToken cancellationToken = default) where T : class;
    Task UpdateAsync<T>(string jobId, BackgroundJobInfo<T> jobInfo, CancellationToken cancellationToken = default) where T : class;
    Task DeleteAsync(string jobId, CancellationToken cancellationToken = default);
}
```

## Job Handler System

### 1. IJobHandler Interface

Base interface for all job handlers:

```csharp
public interface IJobHandler
{
    string JobName { get; }
    Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken);
}
```

### 2. Job Dispatcher

Routes jobs to appropriate handlers:

```csharp
public sealed class JobDispatcher
{
    private readonly IEnumerable<IJobHandler> _handlers;
    private readonly ILogger<JobDispatcher> _logger;

    public async Task DispatchAsync(string jobName, ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var handler = _handlers.FirstOrDefault(h => h.JobName.Equals(jobName, StringComparison.OrdinalIgnoreCase));
        
        if (handler == null)
        {
            _logger.LogWarning("No handler found for job: {JobName}", jobName);
            return;
        }

        try
        {
            await handler.HandleAsync(jobPayload, cancellationToken);
            _logger.LogInformation("Job {JobName} completed successfully", jobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job {JobName}", jobName);
            throw;
        }
    }
}
```

## Built-in Job Types

### 1. Workflow Timeout Jobs

Handles workflow instance timeouts:

```csharp
public sealed class FlowTimeoutJobHandler : IJobHandler
{
    public string JobName => BackgroundJobConsts.FlowTimeoutJobName;

    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var jobData = JsonSerializer.Deserialize<BackgroundJobInfo<WorkflowTimeoutPayload>>(jobPayload.Span);
        if (jobData == null) return;

        var jobInfo = await _jobStore.GetAsync<WorkflowTimeoutPayload>(jobData.JobId, cancellationToken);
        if (jobInfo == null || jobInfo.IsTriggered) return;

        // Mark as triggered to prevent duplicate processing
        jobInfo.IsTriggered = true;
        await _jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);

        // Execute timeout logic
        using (_currentSchema.Change(jobInfo.Payload.FlowName))
        {
            var instance = await _instanceRepository.GetAsync(jobInfo.Payload.InstanceId, cancellationToken);
            
            if (instance.Status == InstanceStatus.Active)
            {
                // Handle timeout - could transition to failed state or trigger timeout transition
                instance.HandleTimeout();
                await _instanceRepository.UpdateAsync(instance, cancellationToken: cancellationToken);
                
                _logger.LogInformation("Workflow timeout processed for instance {InstanceId}", instance.Id);
            }
        }
    }
}
```

### 2. Auto-Transition Jobs

Handles automatic state transitions:

```csharp
public sealed class AutoTransitionJobHandler : IJobHandler
{
    public string JobName => BackgroundJobConsts.AutoTransitionJobName;

    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var jobData = JsonSerializer.Deserialize<BackgroundJobInfo<AutoTransitionPayload>>(jobPayload.Span);
        if (jobData == null) return;

        var jobInfo = await _jobStore.GetAsync<AutoTransitionPayload>(jobData.JobId, cancellationToken);
        if (jobInfo == null || jobInfo.IsTriggered) return;

        // Mark as triggered
        jobInfo.IsTriggered = true;
        await _jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);

        // Execute auto-transitions
        foreach (var transitionKey in jobInfo.Payload.TransitionKeys)
        {
            try
            {
                var response = await _instanceAppService.TransitionAsync(
                    jobInfo.Payload.InstanceId,
                    transitionKey,
                    new TransitionInput
                    {
                        Domain = jobInfo.Payload.Domain,
                        Workflow = jobInfo.Payload.FlowName,
                        Version = jobInfo.Payload.Version,
                        Sync = true
                    },
                    cancellationToken);

                _logger.LogInformation("Auto-transition {TransitionKey} executed for instance {InstanceId}", 
                    transitionKey, jobInfo.Payload.InstanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing auto-transition {TransitionKey} for instance {InstanceId}", 
                    transitionKey, jobInfo.Payload.InstanceId);
            }
        }
    }
}
```

## Workflow-Specific Job Extensions

### 1. Workflow Timeout Extension

```csharp
public static class WorkflowTimeoutJobExtensions
{
    public static Task EnqueueFlowTimeoutAsync(
        this IBackgroundJobService jobService,
        Guid instanceId,
        string flowName,
        string domain,
        string version,
        string timeout,
        CancellationToken cancellationToken = default)
    {
        var jobId = $"timeout-{flowName}-{instanceId}";

        var payload = new WorkflowTimeoutPayload
        {
            Domain = domain,
            InstanceId = instanceId,
            FlowName = flowName,
            Version = version
        };

        return jobService.EnqueueAsync(
            jobName: BackgroundJobConsts.FlowTimeoutJobName,
            jobId: jobId,
            schedule: DaprJobSchedule.FromDateTime(
                DateTime.UtcNow.Add(XmlConvert.ToTimeSpan(timeout))
            ),
            payload: payload,
            cancellationToken: cancellationToken);
    }
}
```

### 2. Auto-Transition Extension

```csharp
public static Task EnqueueAutoTransitionAsync(
    this IBackgroundJobService jobService,
    Guid instanceId,
    string flowName,
    string domain,
    string version,
    string? currentState,
    string[] transitionKeys,
    CancellationToken cancellationToken = default)
{
    var jobId = $"auto-transition-{flowName}-{instanceId}-{currentState ?? "NA"}";

    var payload = new AutoTransitionPayload
    {
        Domain = domain,
        InstanceId = instanceId,
        FlowName = flowName,
        Version = version,
        TransitionKeys = transitionKeys
    };

    return jobService.EnqueueAsync(
        jobName: BackgroundJobConsts.AutoTransitionJobName,
        jobId: jobId,
        schedule: DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(1)), // Execute immediately
        payload: payload,
        cancellationToken: cancellationToken);
}
```

### 3. Timer-Based Transition Extension

```csharp
public static Task EnqueueTransitionTimerAsync(
    this IBackgroundJobService jobService,
    Guid instanceId,
    string flowName,
    string domain,
    string version,
    string transitionKey,
    string timerDuration,
    CancellationToken cancellationToken = default)
{
    var jobId = $"timer-transition-{flowName}-{instanceId}-{transitionKey}";

    var payload = new TransitionTimerPayload
    {
        Domain = domain,
        InstanceId = instanceId,
        FlowName = flowName,
        Version = version,
        TransitionKey = transitionKey
    };

    return jobService.EnqueueAsync(
        jobName: BackgroundJobConsts.TransitionTimerJobName,
        jobId: jobId,
        schedule: DaprJobSchedule.FromDateTime(
            DateTime.UtcNow.Add(XmlConvert.ToTimeSpan(timerDuration))
        ),
        payload: payload,
        cancellationToken: cancellationToken);
}
```

## Job Payload Types

### 1. Background Job Info Wrapper

```csharp
public class BackgroundJobInfo<T> where T : class
{
    public string JobName { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string ExpressionValue { get; set; } = string.Empty;
    public T Payload { get; set; } = default!;
    public bool IsTriggered { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TriggeredAt { get; set; }
}
```

### 2. Workflow Timeout Payload

```csharp
public sealed class WorkflowTimeoutPayload
{
    public string Domain { get; set; } = string.Empty;
    public Guid InstanceId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}
```

### 3. Auto-Transition Payload

```csharp
public sealed class AutoTransitionPayload
{
    public string Domain { get; set; } = string.Empty;
    public Guid InstanceId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string[] TransitionKeys { get; set; } = Array.Empty<string>();
}
```

### 4. Transition Timer Payload

```csharp
public sealed class TransitionTimerPayload
{
    public string Domain { get; set; } = string.Empty;
    public Guid InstanceId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string TransitionKey { get; set; } = string.Empty;
}
```

## DAPR Integration

### 1. DAPR Job Client Configuration

```csharp
public static IServiceCollection AddInfrastructureModule(this IServiceCollection services)
{
    // DAPR Jobs integration
    services.AddDapr();
    services.AddScoped<DaprJobsClient>();
    
    // Background job services
    services.AddScoped<IBackgroundJobService, DaprBackgroundJobService>();
    services.AddScoped<IJobStore, EfCoreJobStore>();
    
    // Job handlers
    services.AddScoped<IJobHandler, FlowTimeoutJobHandler>();
    services.AddScoped<IJobHandler, AutoTransitionJobHandler>();
    services.AddScoped<JobDispatcher>();
    
    return services;
}
```

### 2. DAPR Job Handler Registration

```csharp
public static WebApplication UseApiHostModule(this WebApplication app)
{
    // Register DAPR job handler endpoint
    app.MapDaprScheduledJobHandler(async (string jobName, ReadOnlyMemory<byte> jobPayload, JobDispatcher dispatcher,
        CancellationToken cancellationToken) =>
    {
        await dispatcher.DispatchAsync(jobName, jobPayload, cancellationToken);
    });
    
    return app;
}
```

## Job Scheduling Examples

### 1. Schedule Workflow Timeout

```csharp
// Schedule a workflow timeout for 2 hours
await backgroundJobService.EnqueueFlowTimeoutAsync(
    instanceId: workflowInstance.Id,
    flowName: "loan-approval",
    domain: "banking",
    version: "1.0.0",
    timeout: "PT2H", // 2 hours in ISO 8601 duration format
    cancellationToken: cancellationToken);
```

### 2. Schedule Auto-Transition

```csharp
// Schedule immediate auto-transition for available transitions
await backgroundJobService.EnqueueAutoTransitionAsync(
    instanceId: workflowInstance.Id,
    flowName: "loan-approval",
    domain: "banking",
    version: "1.0.0",
    currentState: "pending-review",
    transitionKeys: new[] { "auto-approve-small-loans" },
    cancellationToken: cancellationToken);
```

### 3. Schedule Timer-Based Transition

```csharp
// Schedule a transition to execute after 30 minutes
await backgroundJobService.EnqueueTransitionTimerAsync(
    instanceId: workflowInstance.Id,
    flowName: "loan-approval",
    domain: "banking",
    version: "1.0.0",
    transitionKey: "timeout-to-review",
    timerDuration: "PT30M", // 30 minutes
    cancellationToken: cancellationToken);
```

### 4. Custom Job Scheduling

```csharp
// Schedule a custom job with cron expression
await backgroundJobService.EnqueueAsync(
    jobName: "daily-report-generation",
    jobId: $"daily-report-{DateTime.UtcNow:yyyyMMdd}",
    schedule: DaprJobSchedule.FromCronExpression("0 9 * * *"), // 9 AM daily
    payload: new ReportGenerationPayload
    {
        ReportType = "daily-summary",
        Domain = "reporting"
    },
    cancellationToken: cancellationToken);
```

## Error Handling and Resilience

### 1. Retry Logic

```csharp
public sealed class FlowTimeoutJobHandler : IJobHandler
{
    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Job retry attempt {RetryCount} after {Delay}ms", 
                        retryCount, timespan.TotalMilliseconds);
                });

        await retryPolicy.ExecuteAsync(async () =>
        {
            // Job execution logic
            await ExecuteJobLogicAsync(jobPayload, cancellationToken);
        });
    }
}
```

### 2. Dead Letter Handling

```csharp
public sealed class DeadLetterJobHandler : IJobHandler
{
    public string JobName => "dead-letter-processor";

    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        try
        {
            var failedJob = JsonSerializer.Deserialize<FailedJobInfo>(jobPayload.Span);
            
            // Log failed job details
            _logger.LogError("Processing dead letter job: {JobName} - {JobId} - {Error}", 
                failedJob.OriginalJobName, failedJob.JobId, failedJob.Error);
            
            // Store in dead letter queue table for manual inspection
            await _deadLetterRepository.SaveAsync(failedJob, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing dead letter job");
        }
    }
}
```

## Job Store Implementation

### 1. Entity Framework Core Job Store

```csharp
public sealed class EfCoreJobStore : IJobStore
{
    private readonly ICurrentSchema _currentSchema;
    private readonly IInstanceJobRepository _jobRepository;
    private readonly IGuidGenerator _guidGenerator;

    public async Task SaveAsync<T>(string jobId, BackgroundJobInfo<T> jobInfo, CancellationToken cancellationToken = default) where T : class
    {
        using (_currentSchema.Change(ExtractSchemaFromJobId(jobId)))
        {
            var existingJob = await _jobRepository.FindByJobIdAsync(jobId, cancellationToken);
            
            if (existingJob != null)
            {
                // Update existing job
                existingJob.UpdatePayload(JsonSerializer.Serialize(jobInfo));
                existingJob.IsTriggered = jobInfo.IsTriggered;
                await _jobRepository.UpdateAsync(existingJob, cancellationToken: cancellationToken);
            }
            else
            {
                // Create new job
                var instanceJob = new InstanceJob(
                    _guidGenerator.Create(),
                    jobInfo.JobName,
                    jobId,
                    ExtractFlowNameFromJobId(jobId),
                    ExtractDomainFromJobId(jobId),
                    ExtractInstanceIdFromJobId(jobId),
                    jobInfo.ExpressionValue,
                    JsonSerializer.Serialize(jobInfo));
                
                await _jobRepository.InsertAsync(instanceJob, cancellationToken: cancellationToken);
            }
        }
    }
}
```

## Monitoring and Observability

### 1. Job Metrics

```csharp
public sealed class JobMetricsMiddleware
{
    private readonly IMetrics _metrics;
    
    public async Task InvokeAsync(string jobName, Func<Task> next)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        
        try
        {
            await next();
            success = true;
        }
        finally
        {
            stopwatch.Stop();
            
            _metrics.CreateCounter("background_jobs_total")
                .WithTag("job_name", jobName)
                .WithTag("success", success.ToString())
                .Add(1);
                
            _metrics.CreateHistogram("background_job_duration_ms")
                .WithTag("job_name", jobName)
                .Record(stopwatch.ElapsedMilliseconds);
        }
    }
}
```

### 2. Health Checks

```csharp
public class BackgroundJobHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check DAPR job client connectivity
            var testJob = new { test = "health_check" };
            var jobId = $"health_check_{Guid.NewGuid()}";
            
            await _daprJobsClient.ScheduleJobAsync(
                "health_check",
                DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                JsonSerializer.SerializeToUtf8Bytes(testJob),
                cancellationToken: cancellationToken);
            
            return HealthCheckResult.Healthy("Background job system is operational");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Background job system is unavailable", ex);
        }
    }
}
```

## Best Practices

### 1. Job Design Guidelines

- **Idempotent Operations**: Ensure jobs can be safely retried
- **Small Payloads**: Keep job payloads minimal and focused
- **Timeout Handling**: Set appropriate timeouts for job execution
- **State Management**: Use job store to track execution state

### 2. Performance Considerations

- **Batch Processing**: Group related operations when possible
- **Resource Management**: Dispose of resources properly in job handlers
- **Memory Usage**: Avoid loading large datasets in memory
- **Connection Pooling**: Reuse database connections efficiently

### 3. Error Handling

- **Graceful Degradation**: Handle failures without affecting other jobs
- **Logging**: Provide detailed logging for troubleshooting
- **Dead Letter Queue**: Implement dead letter handling for failed jobs
- **Monitoring**: Set up alerts for job failures and performance issues

### 4. Security

- **Payload Validation**: Validate job payload data
- **Authorization**: Ensure proper permissions for job operations
- **Audit Trail**: Log job execution for compliance
- **Secret Management**: Use secure storage for sensitive data

The background job system provides a robust foundation for asynchronous workflow processing, enabling complex workflow behaviors while maintaining reliability and performance at scale. 