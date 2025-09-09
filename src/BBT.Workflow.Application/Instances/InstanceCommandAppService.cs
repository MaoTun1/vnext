using BBT.Aether.Application.Services;
using BBT.Aether.DistributedLock;
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
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IStateMachineService stateMachineService,
    IStateMachineExecutor stateMachineExecutor,
    IHeaderService headerService,
    ISubFlowService subFlowService,
    IScriptContextFactory scriptContextFactory,
    IDistributedLockService distributedLockService)
    : ApplicationService(serviceProvider), IInstanceCommandAppService
{
    public async Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var workflow =
                await componentCacheStore.GetFlowAsync(input.Domain, input.Workflow, input.Version, cancellationToken);
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            var initialState = workflow.GetInitialState();

            var instance = await instanceRepository.FindByKeyAsync(input.Instance.Key, cancellationToken)
                           ?? Instance.Create(
                               GuidGenerator.Create(),
                               input.Workflow,
                               input.Instance.Key);

            instance.ChangeState(initialState);
            instance.AddTags(input.Instance.Tags);

            if (instance.IsTransient)
            {
                // Metrics are now automatically recorded by MetricsEnabledInstanceRepository decorator
                await instanceRepository.InsertAsync(instance, true, cancellationToken);
            }

            var scriptContextBuilder = scriptContextFactory.NewBuilder()
                .WithWorkflow(workflow)
                .WithInstance(instance)
                .WithRuntime(runtimeInfoProvider)
                .WithBody(input.Instance.Attributes)
                .WithHeaders(input.Headers)
                .WithRouteValues(input.RouteValues);

            var scriptContext = await scriptContextBuilder
                .WithTransition(workflow.StartTransition).BuildAsync(cancellationToken);

            var transition = await stateMachineService.GetTransitionAsync(
                workflow,
                instance,
                workflow.StartTransition.Key,
                scriptContext,
                input.Instance.Attributes,
                WorkflowExecutionContext.User, // Start transitions are always user-initiated
                cancellationToken
            );

            var data = new JsonData(input.Instance.Attributes);
            if (input.Instance.Attributes != null)
            {
                instance.AddData(
                    GuidGenerator.Create(),
                    data,
                    transition.VersionStrategy
                );
            }

            scriptContext = await scriptContextBuilder
                .WithTransition(transition)
                .BuildAsync(cancellationToken);

            await stateMachineExecutor.ExecuteTransitionAsync(scriptContext, cancellationToken);

            await instanceRepository.UpdateAsync(instance, true, cancellationToken);

            //TODO: Timer reset will be evaluated
            await stateMachineExecutor.FlowTimeoutAsync(workflow, instance, cancellationToken);

            headerService.AddHeader(
                WorkflowInfo.Name,
                WorkflowInfo.Generate(runtimeInfoProvider.Domain, workflow.Key, workflow.Version, instance.Id)
            );

            // Build instance transition information (status, state, and correlations)
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

            return new InstanceServiceResponse<StartInstanceOutput>(new StartInstanceOutput
            {
                Id = instance.Id,
                Status = transitionInfo.Status,
                CurrentState = transitionInfo.CurrentState,
                ActiveCorrelations = transitionInfo.ActiveCorrelations
            });
        }
    }

    public async Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        var resourceId = $"instance-{instanceId}";
        runtimeInfoProvider.Check(input.Domain);

        var lockAcquired = false;
        Instance instance;

        try
        {
            using (currentSchema.Change(input.Workflow))
            {
                // Check if transition should be forwarded to SubFlow instance
                var subFlowResponse = await subFlowService.TryForwardTransitionToSubFlowAsync(instanceId, transitionKey, input, cancellationToken);
                
                if (subFlowResponse != null)
                {
                    // Transition was forwarded to SubFlow, return SubFlow response with main instance ID
                    return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
                    {
                        Id = instanceId, // Keep main instance ID for client
                        Status = subFlowResponse.Data.Status,
                        CurrentState = subFlowResponse.Data.CurrentState,
                        ActiveCorrelations = subFlowResponse.Data.ActiveCorrelations
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

            // Build instance transition information (status, state, and correlations)
            var transitionInfo =
                await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

            headerService.AddHeader(
                WorkflowInfo.Name,
                WorkflowInfo.Generate(runtimeInfoProvider.Domain, workflowForTransitions.Key,
                    workflowForTransitions.Version,
                    instance.Id)
            );

            return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
            {
                Id = instance.Id,
                Status = transitionInfo.Status,
                CurrentState = transitionInfo.CurrentState,
                ActiveCorrelations = transitionInfo.ActiveCorrelations
            });
        }
    }

    /// <summary>
    /// Builds instance transition information including status, current state, and correlations.
    /// This method consolidates the logic for determining instance information based on instance status.
    /// </summary>
    /// <param name="instance">The workflow instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing status, current state, and correlations</returns>
    private async
        Task<(string Status, string? CurrentState, List<InstanceCorrelationInfo>
            ActiveCorrelations)> BuildInstanceTransitionInfoAsync(
            Instance instance,
            CancellationToken cancellationToken = default)
    {
        var status = instance.Status.Description;
        var currentState = instance.CurrentState;
        
        // Get active correlations
        var activeCorrelations = await GetActiveCorrelationsAsync(instance.Id, cancellationToken);

        return (status, currentState, activeCorrelations);
    }

    /// <summary>
    /// Gets active SubFlow/SubProcess correlations for the instance.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active correlation information</returns>
    private async Task<List<InstanceCorrelationInfo>> GetActiveCorrelationsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var correlations =
            await instanceCorrelationRepository.GetActiveByParentAsync(instanceId, cancellationToken);

        return correlations
            .Select(c => new InstanceCorrelationInfo
            {
                CorrelationId = c.Id,
                ParentState = c.ParentState,
                SubFlowInstanceId = c.SubFlowInstanceId,
                SubFlowType = c.SubFlowType.Code,
                SubFlowDomain = c.SubFlowDomain,
                SubFlowName = c.SubFlowName,
                SubFlowVersion = c.SubFlowVersion,
                IsCompleted = c.IsCompleted
            })
            .ToList();
    }


    /// <summary>
    /// Executes the transition logic within the distributed lock context.
    /// This method contains the core transition execution logic that needs to be protected by the lock.
    /// Returns the updated instance after transition execution.
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

            await ExecuteWithBusyStatusAsync(instance, async () =>
            {
                // Get current workflow for transition execution
                var currentWorkflow = await componentCacheStore.GetFlowAsync(
                    input.Domain, 
                    input.Workflow,
                    input.Version, 
                    cancellationToken); 

                var scriptContextBuilder = scriptContextFactory.NewBuilder()
                    .WithWorkflow(currentWorkflow)
                    .WithInstance(instance)
                    .WithRuntime(runtimeInfoProvider)
                    .WithBody(input.Data)
                    .WithHeaders(input.Headers)
                    .WithRouteValues(input.RouteValues);

                var scriptContext = await scriptContextBuilder
                    .WithTransition(currentWorkflow.FindTransition(transitionKey,
                        currentWorkflow.GetState(instance.CurrentState!))!)
                    .BuildAsync(cancellationToken);

            var transition = await stateMachineService.GetTransitionAsync(
                currentWorkflow,
                instance,
                transitionKey,
                scriptContext,
                input.Data,
                input.ExecutionContext,
                cancellationToken
            );

                var data = new JsonData(input.Data);
                if (input.Data != null)
                {
                    instance.AddData(
                        GuidGenerator.Create(),
                        data,
                        transition.VersionStrategy
                    );
                }

                await instanceRepository.UpdateAsync(instance, true, cancellationToken);

                await ExecuteTransitionImmediatelyAsync(
                    transition,
                    scriptContextBuilder,
                    cancellationToken);

                await instanceRepository.UpdateAsync(instance, true, cancellationToken);

                return instance;
            }, cancellationToken);

            return instance;
        }
    }

    /// <summary>
    /// Handles busy status transitions with proper cleanup.
    /// </summary>
    private async Task ExecuteWithBusyStatusAsync<T>(Instance instance,
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Set instance to busy
        instance.Busy();
        await instanceRepository.UpdateAsync(instance, true, cancellationToken);

        try
        {
            // Execute the operation
            await operation();
        }
        finally
        {
            // Always reset to active, even on failure
            try
            {
                if (!instance.Status.Equals(InstanceStatus.Completed))
                {
                    instance.Active();
                    await instanceRepository.UpdateAsync(instance, true, cancellationToken);
                }
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

    /// <summary>
    /// Handles busy status transitions with proper cleanup for void operations.
    /// </summary>
    private async Task ExecuteWithBusyStatusAsync(
        Instance instance,
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        // Set instance to busy
        instance.Busy();
        await instanceRepository.UpdateAsync(instance, true, cancellationToken);

        try
        {
            // Execute the operation
            await operation();
        }
        finally
        {
            // Always reset to active, even on failure
            try
            {
                if (!instance.Status.Equals(InstanceStatus.Completed))
                {
                    instance.Active();
                    await instanceRepository.UpdateAsync(instance, true, cancellationToken);
                }
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

    private async Task ExecuteTransitionImmediatelyAsync(
        Transition transition,
        IScriptContextBuilder scriptContextBuilder,
        CancellationToken cancellationToken = default)
    {
        var scriptContext = await scriptContextBuilder
            .WithTransition(transition)
            .BuildAsync(cancellationToken);

        await stateMachineExecutor.ExecuteTransitionAsync(scriptContext, cancellationToken);
    }
}