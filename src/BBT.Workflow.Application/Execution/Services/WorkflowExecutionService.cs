using BBT.Aether.DistributedLock;
using BBT.Aether.Guids;
using BBT.Workflow.Caching;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Execution.Strategies;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.SubFlow;
using Microsoft.Extensions.Logging;
using ExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Service implementation for orchestrating workflow execution operations.
/// Coordinates between validation, preparation, and execution strategies.
/// </summary>
public sealed class WorkflowExecutionService(
    IExecutionStrategyFactory strategyFactory,
    ICurrentSchema currentSchema,
    ISchemaManager schemaManager,
    IRuntimeInfoProvider runtimeInfoProvider,
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository,
    IStateMachineExecutor stateMachineExecutor,
    IGuidGenerator guidGenerator,
    ISubFlowService subFlowService,
    IDistributedLockService distributedLockService,
    ILogger<WorkflowExecutionService> logger) : IWorkflowExecutionService
{
    /// <inheritdoc />
    public async Task<InstanceServiceResponse<StartInstanceOutput>> ExecuteStartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate runtime and prepare schema
        runtimeInfoProvider.Check(input.Domain);

        using (currentSchema.Change(input.Workflow))
        {
            // Load workflow and ensure schema
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);

            // Check initial state
            workflow.GetInitialState();

            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            // Create and prepare instance
            var instance = await CreateAndPrepareInstanceAsync(
                workflow,
                input.Instance.Id ?? guidGenerator.Create(),
                input.Instance.Key,
                input.Instance.Tags?.ToList(),
                input.Instance.MetaData,
                input.Sync,
                input.Instance.Callback,
                cancellationToken);

            // Validate transition and build script context
            (var transition, var scriptContextBuilder) = await stateMachineExecutor.ValidateTransitionAsync(
                workflow,
                instance,
                workflow.StartTransition.Key,
                input.Instance.Attributes,
                input.Headers.ToDictionary(h => h.Key, h => h.Value),
                input.RouteValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                ExecutionContext.User,
                cancellationToken
            );

            // Add instance data if provided
            if (input.Instance.Attributes != null)
            {
                var data = new JsonData(input.Instance.Attributes);
                instance.AddData(
                    guidGenerator.Create(),
                    data,
                    transition.VersionStrategy
                );
            }

            // Persist new instance
            await instanceRepository.InsertAsync(instance, true, cancellationToken);
            logger.LogDebug("Created new instance {InstanceId} with key {InstanceKey}",
                instance.Id, instance.Key);

            // Schedule flow timeout if configured
            await stateMachineExecutor.FlowTimeoutAsync(workflow, instance, cancellationToken);

            // Create execution context
            var context = new InstanceStartExecutionContext(
                input, workflow, instance, transition, scriptContextBuilder);

            // Get and execute strategy
            var strategy = strategyFactory.GetInstanceStartStrategy(input.Sync);
            return await strategy.ExecuteAsync(context, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<InstanceServiceResponse<TransitionOutput>> ExecuteTransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);

        using (currentSchema.Change(input.Workflow))
        {
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

                // Load workflow and ensure schema
                var workflow = await componentCacheStore.GetFlowAsync(
                    input.Domain, input.Workflow, input.Version, cancellationToken);
                await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

                // Get and validate instance
                var instance = await instanceRepository.GetActiveAsync(instanceId, cancellationToken);

                // Validate transition and build script context
                (var transition, var scriptContextBuilder) = await stateMachineExecutor.ValidateTransitionAsync(
                    workflow,
                    instance,
                    transitionKey,
                    input.Data,
                    input.Headers,
                    input.RouteValues,
                    input.ExecutionContext,
                    cancellationToken
                );

                // Create execution context
                var context = new TransitionExecutionContext(
                    instanceId, transitionKey, input, workflow, instance, transition, scriptContextBuilder);

                // Get and execute strategy
                var strategy = strategyFactory.GetTransitionStrategy(input.Sync);
                return await strategy.ExecuteAsync(context, cancellationToken);
            }
            finally
            {
                if (lockAcquired)
                {
                    // Always release the lock
                    try
                    {
                        await distributedLockService.ReleaseLockAsync(resourceId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed to release distributed lock for instance {InstanceId} transition {TransitionKey}",
                            instanceId, transitionKey);
                    }
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

        return instance;
    }
}