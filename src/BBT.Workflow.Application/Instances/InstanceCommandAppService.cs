using BBT.Aether.Application.Services;
using BBT.Aether.DistributedLock;
using BBT.Aether.Guids;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Headers;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Scripting;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;
using BBT.Workflow.States;
using BBT.Workflow.SubFlow;
using Dapr.Jobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Instances;

public sealed class InstanceCommandAppService(
    IServiceProvider serviceProvider,
    ICurrentSchema currentSchema,
    ISchemaManager schemaManager,
    IRuntimeInfoProvider runtimeInfoProvider,
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository,
    IStateMachineExecutor stateMachineExecutor,
    IHeaderService headerService,
    ISubFlowService subFlowService,
    IDistributedLockService distributedLockService,
    IBackgroundJobService backgroundJobService,
    IGuidGenerator guidGenerator)
    : ApplicationService(serviceProvider), IInstanceCommandAppService
{
    public async Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate runtime and prepare schema
        runtimeInfoProvider.Check(input.Domain);

        // If Sync=false, enqueue as background job
        if (!input.Sync)
        {
            return await EnqueueStartInstanceJobAsync(input, cancellationToken);
        }

        using (currentSchema.Change(input.Workflow))
        {
            // Load workflow and ensure schema
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            input.Instance.MetaData.Add(DomainConsts.MetaDataKeys.Sync, input.Sync.ToString().ToLower());
            input.Instance.MetaData.Add(DomainConsts.MetaDataKeys.Callback, input.Instance.Callback);
            // Delegate instance creation and execution to StateMachineExecutor
            var instance = await stateMachineExecutor.StartInstanceAsync(
                workflow,
                input.Instance.Id ?? GuidGenerator.Create(),
                input.Instance.Key,
                input.Instance.Tags?.ToList(),
                input.Instance.MetaData,
                input.Instance.Attributes,
                input.Headers,
                input.RouteValues?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
                WorkflowExecutionContext.User,
                cancellationToken);

            // Add workflow information to response headers
            headerService.AddHeader(
                WorkflowInfo.Name,
                WorkflowInfo.Generate(runtimeInfoProvider.Domain, workflow.Key, workflow.Version, instance.Id)
            );

            // Build and return response
            return new InstanceServiceResponse<StartInstanceOutput>(new StartInstanceOutput
            {
                Id = instance.Id,
                Status = instance.Status
            });
        }
    }

    public async Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);

        // If Sync=false, enqueue as background job
        if (!input.Sync)
        {
            return await EnqueueTransitionJobAsync(instanceId, transitionKey, input, cancellationToken);
        }

        var resourceId = $"instance-{instanceId}";
        var lockAcquired = false;
        Instance instance;

        try
        {
            using (currentSchema.Change(input.Workflow))
            {
                // Check if transition should be forwarded to SubFlow instance
                var subFlowResponse =
                    await subFlowService.TryForwardTransitionToSubFlowAsync(instanceId, transitionKey, input,
                        cancellationToken);

                if (subFlowResponse != null)
                {
                    // Transition was forwarded to SubFlow, return SubFlow response with main instance ID
                    return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
                    {
                        Id = instanceId, // Keep main instance ID for client
                        Status = subFlowResponse.Data.Status
                    });
                }

                // Process transition locally lock for the instance
                // Acquire distributed 
                lockAcquired = await distributedLockService.TryAcquireLockAsync(
                    resourceId,
                    InstanceConstants.TransitionLockExpiryInSeconds,
                    cancellationToken);

                if (!lockAcquired)
                {
                    throw new TransitionLockedException(instanceId, transitionKey);
                }

                // Execute transition within lock
                instance = await ExecuteTransitionWithinLockAsync(instanceId, transitionKey, input, cancellationToken);
            }
        }
        finally
        {
            // Ensure lock is always released if it was acquired
            if (lockAcquired)
            {
                try
                {
                    await distributedLockService.ReleaseLockAsync(resourceId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log but don't throw - we don't want to mask the original exception
                    Logger.LogWarning(ex,
                        "Failed to release distributed lock for instance {InstanceId} transition {TransitionKey}",
                        instanceId, transitionKey);
                }
            }
        }

        // After lock is released, calculate available transitions to reflect the true state
        using (currentSchema.Change(input.Workflow))
        {
            // Get workflow for available transitions (may have changed after transition execution)
            var workflowForTransitions = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow,
                input.Version, cancellationToken);

            headerService.AddHeader(
                WorkflowInfo.Name,
                WorkflowInfo.Generate(runtimeInfoProvider.Domain, workflowForTransitions.Key,
                    workflowForTransitions.Version,
                    instance.Id)
            );

            return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
            {
                Id = instance.Id,
                Status = instance.Status
            });
        }
    }

    /// <summary>
    /// Executes the transition logic within the distributed lock context.
    /// This method delegates the actual transition execution to StateMachineExecutor.
    /// </summary>
    private async Task<Instance> ExecuteTransitionWithinLockAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(input.Workflow))
        {
            var instance = await instanceRepository.GetActiveAsync(instanceId, cancellationToken);
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);

            await ExecuteWithBusyStatusAsync(instance, input.ExecutionContext, async () =>
            {
                // Delegate to StateMachineExecutor for actual transition execution
                // StateMachineExecutor handles instance persistence internally
                await stateMachineExecutor.ExecuteManualTransitionAsync(
                    workflow,
                    instance,
                    transitionKey,
                    input.Data,
                    input.Headers,
                    input.RouteValues?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
                    input.ExecutionContext,
                    cancellationToken);

                return instance;
            }, cancellationToken);

            return instance;
        }
    }

    /// <summary>
    /// Handles busy status transitions with proper cleanup.
    /// </summary>
    private async Task ExecuteWithBusyStatusAsync<T>(Instance instance,
        WorkflowExecutionContext executionContext,
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (executionContext == WorkflowExecutionContext.User)
        {
            // Set instance to busy
            instance.Busy();
            await instanceRepository.UpdateAsync(instance, true, cancellationToken);
        }

        try
        {
            // Execute the operation
            await operation();
        }
        finally
        {
            if (executionContext == WorkflowExecutionContext.User)
            {
                // Always reset to active, even on failure
                try
                {
                    if (!instance.Status.Equals(InstanceStatus.Completed))
                    {
                        instance.Active();
                        await instanceRepository.UpdateAsync(instance, true, cancellationToken);
                    }
                    // // Reload instance from database to avoid concurrency conflicts
                    // var currentInstance = await instanceRepository.FindAsync(instance.Id, includeDetails: false, cancellationToken);
                    // if (currentInstance != null && !currentInstance.Status.Equals(InstanceStatus.Completed))
                    // {
                    //     currentInstance.Active();
                    //     await instanceRepository.UpdateAsync(currentInstance, true, cancellationToken);
                    // }
                }
                catch (Exception ex)
                {
                    // Log but don't throw - we don't want to mask the original exception
                    Logger.LogWarning(ex,
                        "Failed to reset instance {InstanceId} status to Active after transition processing",
                        instance.Id);
                }
            }
        }
    }

    /// <summary>
    /// Handles busy status transitions with proper cleanup for void operations.
    /// </summary>
    private async Task ExecuteWithBusyStatusAsync(
        Instance instance,
        WorkflowExecutionContext executionContext,
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (executionContext == WorkflowExecutionContext.User)
        {
            // Set instance to busy
            instance.Busy();
            await instanceRepository.UpdateAsync(instance, true, cancellationToken);
        }

        try
        {
            // Execute the operation
            await operation();
        }
        finally
        {
            if (executionContext == WorkflowExecutionContext.User)
            {
                // Always reset to active, even on failure
                try
                {
                    if (!instance.Status.Equals(InstanceStatus.Completed))
                    {
                        instance.Active();
                        await instanceRepository.UpdateAsync(instance, true, cancellationToken);
                    }
                    // Reload instance from database to avoid concurrency conflicts
                    // var currentInstance = await instanceRepository.FindAsync(instance.Id, includeDetails: false, cancellationToken);
                    // if (currentInstance != null && !currentInstance.Status.Equals(InstanceStatus.Completed))
                    // {
                    //     currentInstance.Active();
                    //     await instanceRepository.UpdateAsync(currentInstance, true, cancellationToken);
                    // }
                }
                catch (Exception ex)
                {
                    // Log but don't throw - we don't want to mask the original exception
                    Logger.LogWarning(ex,
                        "Failed to reset instance {InstanceId} status to Active after transition processing",
                        instance.Id);
                }
            }
        }
    }

    /// <summary>
    /// Enqueues a start instance operation as a background job when Sync=true.
    /// </summary>
    private async Task<InstanceServiceResponse<StartInstanceOutput>> EnqueueStartInstanceJobAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(input.Workflow))
        {
            var instanceId = input.Instance.Id ?? guidGenerator.Create();
            var jobId = $"start-instance-{instanceId}";

            // Create job payload
            var payload = new StartInstanceJobPayload
            {
                Domain = input.Domain,
                Workflow = input.Workflow,
                Version = input.Version,
                InstanceId = instanceId,
                InstanceKey = input.Instance.Key,
                Tags = input.Instance.Tags,
                Attributes = input.Instance.Attributes,
                Callback = input.Instance.Callback,
                MetaData = input.Instance.MetaData.ToDictionary(kvp => kvp.Key,
                    kvp => kvp.Value ?? (object)string.Empty),
                Headers = input.Headers,
                RouteValues = input.RouteValues
            };

            // Add sync metadata
            payload.MetaData.Add(DomainConsts.MetaDataKeys.Sync, input.Sync.ToString().ToLower());
            payload.MetaData.Add(DomainConsts.MetaDataKeys.Callback, input.Instance.Callback ?? string.Empty);

            // Create job metadata
            var jobMetadata = new Dictionary<string, string>
            {
                ["domain"] = input.Domain,
                ["flowName"] = input.Workflow,
                ["instanceId"] = instanceId.ToString()
            };

            // Schedule job to run immediately
            var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(1));

            await backgroundJobService.EnqueueAsync(
                BackgroundJobConsts.StartInstanceJobName,
                jobId,
                schedule,
                payload,
                jobMetadata,
                cancellationToken);

            Logger.LogInformation(
                "Enqueued start instance job {JobId} for workflow {Workflow} with instance {InstanceId}",
                jobId, input.Workflow, instanceId);

            // Return response with instance ID and Busy status to indicate background processing
            return new InstanceServiceResponse<StartInstanceOutput>(new StartInstanceOutput
            {
                Id = instanceId,
                Status = InstanceStatus.Busy // Indicate that the instance is being processed in background
            });
        }
    }

    /// <summary>
    /// Enqueues a transition operation as a background job when Sync=true.
    /// </summary>
    private async Task<InstanceServiceResponse<TransitionOutput>> EnqueueTransitionJobAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(input.Workflow))
        {
            var jobId = $"transition-{instanceId}-{transitionKey}-{DateTimeOffset.UtcNow.Ticks}";

            // Create job payload
            var payload = new TransitionJobPayload
            {
                InstanceId = instanceId,
                TransitionKey = transitionKey,
                Domain = input.Domain,
                Workflow = input.Workflow,
                Version = input.Version,
                Data = input.Data,
                Headers = input.Headers,
                RouteValues = input.RouteValues,
                ExecutionContext = input.ExecutionContext
            };

            // Create job metadata
            var jobMetadata = new Dictionary<string, string>
            {
                ["domain"] = input.Domain,
                ["flowName"] = input.Workflow,
                ["instanceId"] = instanceId.ToString()
            };

            // Schedule job to run immediately
            var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(1));

            await backgroundJobService.EnqueueAsync(
                BackgroundJobConsts.TransitionJobName,
                jobId,
                schedule,
                payload,
                jobMetadata,
                cancellationToken);

            Logger.LogInformation(
                "Enqueued transition job {JobId} for instance {InstanceId} with transition {TransitionKey}",
                jobId, instanceId, transitionKey);

            // Return response with Busy status to indicate background processing
            return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
            {
                Id = instanceId,
                Status = InstanceStatus.Busy // Indicate that the transition is being processed in background
            });
        }
    }
}