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

        // If Sync=false, validate and prepare then enqueue as background job
        if (!input.Sync)
        {
            return await ValidateAndEnqueueStartInstanceJobAsync(input, cancellationToken);
        }

        using (currentSchema.Change(input.Workflow))
        {
            // Load workflow and ensure schema
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            // Create and prepare instance
            var instance = await CreateAndPrepareInstanceAsync(
                workflow,
                input.Instance.Id ?? GuidGenerator.Create(),
                input.Instance.Key,
                input.Instance.Tags?.ToList(),
                input.Instance.MetaData,
                input.Sync,
                input.Instance.Callback,
                cancellationToken);

            // Execute start transition via StateMachineExecutor
            await stateMachineExecutor.ExecuteInstanceStartAsync(
                workflow,
                instance,
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

        // If Sync=false, validate and prepare then enqueue as background job
        if (!input.Sync)
        {
            return await ValidateAndEnqueueTransitionJobAsync(instanceId, transitionKey, input, cancellationToken);
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
    /// This method handles instance validation and delegates transition execution to StateMachineExecutor.
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
    /// Creates and prepares an instance for execution, handling both new creation and existing instance retrieval.
    /// Prevents duplicate active instances with the same key.
    /// </summary>
    private async Task<Instance> CreateAndPrepareInstanceAsync(
        Definitions.Workflow workflow,
        Guid instanceId,
        string instanceKey,
        List<string>? tags,
        ObjectDictionary metadata,
        bool isSync,
        string? callback,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(instanceKey))
        {
            throw new ArgumentException("Instance key cannot be null or empty", nameof(instanceKey));
        }

        var initialState = workflow.GetInitialState();

        // Check for existing instance
        var existingInstance = await instanceRepository.FindByKeyAsReadOnlyAsync(instanceKey, cancellationToken);
        
        // If instance exists and is not completed, throw conflict exception
        if (existingInstance is { IsCompleted: false })
        {
            throw new ConflictException();
        }

        // Create new instance (existing instance would be completed at this point, so we create new one)
        var instance = Instance.Create(instanceId, workflow.Key, instanceKey);

        // Set metadata
        metadata.TryAdd(DomainConsts.MetaDataKeys.Sync, isSync.ToString().ToLower());
        metadata.TryAdd(DomainConsts.MetaDataKeys.Callback, callback ?? string.Empty);
        instance.SetMetaData(metadata);

        // Initialize instance state and tags (always for new instances)
        instance.ChangeState(initialState);
        
        if (tags?.Any() == true)
        {
            instance.AddTags(tags.ToArray());
        }

        // Persist new instance
        await instanceRepository.InsertAsync(instance, true, cancellationToken);
        Logger.LogDebug("Created new instance {InstanceId} with key {InstanceKey}", 
            instance.Id, instanceKey);

        return instance;
    }

    /// <summary>
    /// Validates instance creation prerequisites and enqueues a start instance operation as a background job when Sync=false.
    /// </summary>
    private async Task<InstanceServiceResponse<StartInstanceOutput>> ValidateAndEnqueueStartInstanceJobAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(input.Workflow))
        {
            // 1. Load workflow and ensure schema - validate that workflow exists
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            // 2. Generate or use provided instance ID
            var instanceId = input.Instance.Id ?? guidGenerator.Create();
            
            // 3. Create and prepare instance for background processing (includes duplicate key check)
            var instance = await CreateAndPrepareInstanceAsync(
                workflow,
                instanceId,
                input.Instance.Key,
                input.Instance.Tags?.ToList(),
                input.Instance.MetaData,
                input.Sync,
                input.Instance.Callback,
                cancellationToken);

            // 4. Set instance to busy to reserve it for background processing
            instance.Busy();
            await instanceRepository.UpdateAsync(instance, true, cancellationToken);

            Logger.LogInformation(
                "Pre-created instance {InstanceId} with key {InstanceKey} for async processing",
                instanceId, input.Instance.Key);

            // 5. Now enqueue the background job
            return await EnqueueStartInstanceJobAsync(input, instanceId, cancellationToken);
        }
    }

    /// <summary>
    /// Enqueues a start instance operation as a background job when Sync=false.
    /// </summary>
    private async Task<InstanceServiceResponse<StartInstanceOutput>> EnqueueStartInstanceJobAsync(
        StartInstanceInput input,
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(input.Workflow))
        {
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

            // Ensure sync metadata is set
            if (!payload.MetaData.ContainsKey(DomainConsts.MetaDataKeys.Sync))
            {
                payload.MetaData.Add(DomainConsts.MetaDataKeys.Sync, input.Sync.ToString().ToLower());
            }
            if (!payload.MetaData.ContainsKey(DomainConsts.MetaDataKeys.Callback))
            {
                payload.MetaData.Add(DomainConsts.MetaDataKeys.Callback, input.Instance.Callback ?? string.Empty);
            }

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
    /// Validates transition prerequisites and enqueues a transition operation as a background job when Sync=false.
    /// </summary>
    private async Task<InstanceServiceResponse<TransitionOutput>> ValidateAndEnqueueTransitionJobAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(input.Workflow))
        {
            // 1. Check if transition should be forwarded to SubFlow instance first
            var subFlowResponse = await subFlowService.TryForwardTransitionToSubFlowAsync(
                instanceId, transitionKey, input, cancellationToken);

            if (subFlowResponse != null)
            {
                // Transition was forwarded to SubFlow, return SubFlow response with main instance ID
                return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
                {
                    Id = instanceId, // Keep main instance ID for client
                    Status = subFlowResponse.Data.Status
                });
            }

            // 2. Load workflow and ensure schema
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            // 3. Get and validate instance exists and is in valid state
            var instance = await instanceRepository.GetActiveAsync(instanceId, cancellationToken);
            
            // 4. Validate transition exists and is available from current state
            var currentState = workflow.GetState(instance.CurrentState!);
            var availableTransition = workflow.FindTransition(transitionKey, currentState);
            if (availableTransition == null)
            {
                throw new InvalidStateException(transitionKey, instance.CurrentState);
            }

            // 5. Try to acquire lock to ensure no concurrent processing
            var resourceId = $"instance-{instanceId}";
            var lockAcquired = await distributedLockService.TryAcquireLockAsync(
                resourceId,
                InstanceConstants.TransitionLockExpiryInSeconds,
                cancellationToken);

            if (!lockAcquired)
            {
                throw new TransitionLockedException(instanceId, transitionKey);
            }

            try
            {
                // 6. Double-check instance state after acquiring lock
                var lockedInstance = await instanceRepository.GetActiveAsync(instanceId, cancellationToken);
                if (lockedInstance == null || lockedInstance.Status.Equals(InstanceStatus.Busy))
                {
                    throw new InvalidOperationException(
                        $"Instance {instanceId} is not available for transition (Status: {lockedInstance?.Status})");
                }

                // 7. Set instance to busy to reserve it for background processing
                lockedInstance.Busy();
                await instanceRepository.UpdateAsync(lockedInstance, true, cancellationToken);

                Logger.LogInformation(
                    "Reserved instance {InstanceId} for async transition {TransitionKey}",
                    instanceId, transitionKey);

                // 8. Now enqueue the background job
                return await EnqueueTransitionJobAsync(instanceId, transitionKey, input, cancellationToken);
            }
            finally
            {
                // Always release the lock
                try
                {
                    await distributedLockService.ReleaseLockAsync(resourceId, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex,
                        "Failed to release distributed lock for instance {InstanceId} during async validation",
                        instanceId);
                }
            }
        }
    }

    /// <summary>
    /// Enqueues a transition operation as a background job when Sync=false.
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

    /// <summary>
    /// Executes a start instance operation for background jobs.
    /// This method is specifically designed for background job execution and handles pre-created instances.
    /// </summary>
    public async Task<InstanceServiceResponse<StartInstanceOutput>> ExecuteBackgroundStartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate runtime
        runtimeInfoProvider.Check(input.Domain);
        
        using (currentSchema.Change(input.Workflow))
        {
            // Load workflow and ensure schema
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            var instance = await instanceRepository.GetActiveAsync(input.Instance!.Id.Value, cancellationToken);

            // Execute start transition via StateMachineExecutor
            await stateMachineExecutor.ExecuteInstanceStartAsync(
                workflow,
                instance,
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

    /// <summary>
    /// Executes a manual transition operation for background jobs.
    /// This method is specifically designed for background job execution and handles pre-reserved instances.
    /// </summary>
    public async Task<InstanceServiceResponse<TransitionOutput>> ExecuteBackgroundTransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate runtime
        runtimeInfoProvider.Check(input.Domain);

        // Force sync=true for background execution to avoid infinite loops
        var syncInput = new TransitionInput(
            input.Domain,
            input.Workflow,
            input.Version,
            input.Data,
            sync: true)
        {
            Headers = input.Headers,
            RouteValues = input.RouteValues,
            ExecutionContext = input.ExecutionContext
        };

        return await TransitionAsync(instanceId, transitionKey, syncInput, cancellationToken);
    }
}