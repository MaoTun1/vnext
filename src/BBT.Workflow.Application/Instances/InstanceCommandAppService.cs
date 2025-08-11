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

            // Build instance transition information (status, state, and available transitions)
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, workflow, cancellationToken);

            return new InstanceServiceResponse<StartInstanceOutput>(new StartInstanceOutput
            {
                Id = instance.Id,
                Status = transitionInfo.Status,
                CurrentState = transitionInfo.CurrentState,
                AvailableTransitions = transitionInfo.AvailableTransitions
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
            // Acquire distributed lock for the instance
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
            var workflowForTransitions = await GetCurrentWorkflowAsync(instanceId, input.Domain, input.Workflow,
                input.Version, cancellationToken);

            // Build instance transition information (status, state, and available transitions)
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, workflowForTransitions, cancellationToken);

            headerService.AddHeader(
                WorkflowInfo.Name,
                WorkflowInfo.Generate(runtimeInfoProvider.Domain, workflowForTransitions.Key, workflowForTransitions.Version,
                    instance.Id)
            );

            return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
            {
                Id = instance.Id,
                Status = transitionInfo.Status,
                CurrentState = transitionInfo.CurrentState,
                AvailableTransitions = transitionInfo.AvailableTransitions
            });
        }
    }

    /// <summary>
    /// Ensures there are no blocking SubFlows that would prevent transition execution.
    /// </summary>
    private async Task EnsureNoBlockingSubFlowsAsync(
        Guid instanceId,
        string transitionKey,
        CancellationToken cancellationToken = default)
    {
        var activeSubFlowContext = await subFlowService.GetActiveSubFlowContextAsync(instanceId, cancellationToken);

        if (!activeSubFlowContext.HasValue)
        {
            var hasBlockingSubFlows = await subFlowService.HasBlockingSubFlowsAsync(instanceId, cancellationToken);
            if (hasBlockingSubFlows)
            {
                throw new SubFlowBlockedException(instanceId, transitionKey, 1);
            }
        }
    }

    /// <summary>
    /// Gets the current workflow definition. Since SubFlow now creates separate instances,
    /// this method always returns the main workflow definition.
    /// </summary>
    private async Task<Definitions.Workflow> GetCurrentWorkflowAsync(
        Guid instanceId,
        string domain,
        string workflowName,
        string? version,
        CancellationToken cancellationToken = default)
    {
        // Since SubFlow now creates separate instances via remote calls,
        // the parent instance always uses the main workflow definition
        return await componentCacheStore.GetFlowAsync(domain, workflowName, version, cancellationToken);
    }

    /// <summary>
    /// Builds instance transition information including status, current state, and available user transitions.
    /// This method consolidates the logic for determining available transitions based on instance status.
    /// </summary>
    /// <param name="instance">The workflow instance</param>
    /// <param name="currentWorkflow">The current workflow definition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing status, current state, and available user transitions</returns>
    private async Task<(string Status, string? CurrentState, List<string> AvailableTransitions)> BuildInstanceTransitionInfoAsync(
        Instance instance,
        Definitions.Workflow currentWorkflow,
        CancellationToken cancellationToken = default)
    {
        var status = instance.Status.Description;
        var currentState = instance.CurrentState;
        var availableTransitions = new List<string>();

        // If instance is active, return user-triggered transitions
        if (instance.Status.Equals(InstanceStatus.Active))
        {
            availableTransitions = await GetAvailableUserTransitionsAsync(instance, currentWorkflow, cancellationToken);
        }
        // For other statuses (Busy, Completed, Faulted, Passive), no transitions are available

        return (status, currentState, availableTransitions);
    }

    /// <summary>
    /// Gets available user-triggered transitions for the current instance.
    /// Since SubFlow now creates separate instances, this method always returns main workflow transitions.
    /// However, if the instance is blocked by a SubFlow, no transitions are available.
    /// </summary>
    private async Task<List<string>> GetAvailableUserTransitionsAsync(
        Instance instance,
        Definitions.Workflow currentWorkflow,
        CancellationToken cancellationToken = default)
    {
        // Check if the instance is blocked by a SubFlow
        var hasBlockingSubFlows = await subFlowService.HasBlockingSubFlowsAsync(instance.Id, cancellationToken);

        if (hasBlockingSubFlows)
        {
            // If the instance is blocked by a SubFlow, no transitions are available
            return new List<string>();
        }

        // Return main workflow user transitions
        return stateMachineService.AvailableUserTransitionKeys(currentWorkflow, instance);
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
                // Check for blocking SubFlows that prevent transition execution
                await EnsureNoBlockingSubFlowsAsync(instanceId, transitionKey, cancellationToken);

                // Get current workflow for transition execution
                var currentWorkflow = await GetCurrentWorkflowAsync(instanceId, input.Domain, input.Workflow,
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