using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Remote;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.SubFlow;

/// <summary>
/// Service for managing SubFlow and SubProcess workflows.
/// This service handles the execution and correlation management
/// between parent workflow and SubFlow definitions without creating separate instances.
/// SubFlow states and transitions are managed within the main instance context.
/// </summary>
/// <param name="componentCacheStore">Cache store for retrieving workflow definitions.</param>
/// <param name="instanceCorrelationRepository">Repository for managing instance correlations.</param>
/// <param name="guidGenerator">Service for generating unique identifiers.</param>
/// <param name="remoteInstanceCommandAppService">Manages remote requests to trigger SubFlow.</param>
public sealed class SubFlowService(
    IComponentCacheStore componentCacheStore,
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IGuidGenerator guidGenerator,
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService) : ISubFlowService
{
    /// <summary>
    /// Handles the initiation of SubFlow execution.
    /// Both SubFlow and SubProcess now create separate instances via remote calls.
    /// </summary>
    /// <param name="parentInstance">The main workflow instance that initiates the sub-flow.</param>
    /// <param name="targetState">The target state containing SubFlow configuration.</param>
    /// <param name="context">The script context containing execution data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous sub-flow initiation operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the SubFlow configuration is invalid.</exception>
    /// <remarks>
    /// <para>
    /// SubFlow (Type: "S"): Creates a separate instance and blocks the parent workflow until completion. 
    /// The parent workflow cannot continue until the SubFlow is completed.
    /// </para>
    /// <para>
    /// SubProcess (Type: "P"): Creates a separate instance and runs in parallel without blocking the parent workflow. 
    /// The parent workflow can continue immediately after starting the SubProcess.
    /// </para>
    /// </remarks>
    public async Task HandleSubFlowAsync(
        Instance parentInstance,
        State targetState,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        if (targetState.SubFlow == null)
        {
            throw new InvalidOperationException(
                $"State \"{targetState.Key}\" is marked as SubFlow but has no SubFlow configuration.");
        }

        var subFlowConfig = targetState.SubFlow;

        // Load the sub-workflow definition
        var subWorkflow = await componentCacheStore.GetFlowAsync(
            subFlowConfig.Process.Domain,
            subFlowConfig.Process.Key,
            subFlowConfig.Process.Version,
            cancellationToken);

        // Both SubFlow (Type "S") and SubProcess (Type "P") now create separate instances via remote calls
        // The difference is that SubFlow blocks the parent workflow while SubProcess runs in parallel
        await HandleSubFlowWithRemoteCallAsync(parentInstance, targetState, subWorkflow, context, cancellationToken);
    }

    /// <summary>
    /// Handles both SubFlow and SubProcess execution by creating separate instances via remote calls.
    /// SubFlow blocks the parent workflow while SubProcess runs in parallel.
    /// </summary>
    private async Task HandleSubFlowWithRemoteCallAsync(
        Instance parentInstance,
        State targetState,
        Definitions.Workflow subWorkflow,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var subFlowConfig = targetState.SubFlow!;

        // Get safely converted properties from context
        var (body, headers, routeValues) = context.GetSafeProperties();

        // Generate appropriate key based on SubFlow type
        var instanceKeySuffix = subFlowConfig.Type.Code == "S" ? "subflow" : "subprocess";

        var subFlowStartInput = new StartInstanceInput(
                subFlowConfig.Process.Domain,
                subFlowConfig.Process.Key,
                subFlowConfig.Process.Version,
                false)
        {
            Instance = new CreateInstanceInput
            {
                Key = parentInstance.Key ?? $"{parentInstance.Key}-{instanceKeySuffix}-{targetState.Key}-{guidGenerator.Create()}",
                Attributes = body,
                Tags =
                    [
                        $"parent:{parentInstance.Id}",
                        $"parent-state:{targetState.Key}",
                        $"type:{subFlowConfig.Type.Code}"
                    ]
            },
            Headers = headers,
            RouteValues = routeValues
        };

        var subFlowResult = await remoteInstanceCommandAppService.StartAsync(subFlowStartInput, cancellationToken);

        var correlation = new InstanceCorrelation(
            guidGenerator.Create(),
            parentInstance.Id,
            targetState.Key,
            subFlowResult.Data.Id,
            subFlowConfig.Type.Code,
            subFlowConfig.Process.Domain,
            subFlowConfig.Process.Key,
            subFlowConfig.Process.Version);

        await instanceCorrelationRepository.InsertAsync(correlation, true, cancellationToken);
    }

    /// <summary>
    /// Checks if a SubFlow instance has completed by querying its status.
    /// This is a placeholder method that will be implemented with a dedicated endpoint.
    /// </summary>
    /// <param name="subFlowInstanceId">The SubFlow instance ID to check for completion.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the SubFlow has completed; otherwise, false.</returns>
    /// <remarks>
    /// TODO: Implement this method to call a dedicated endpoint that checks SubFlow completion status.
    /// This endpoint will be responsible for determining if a SubFlow instance has reached a completion state.
    /// </remarks>
    public async Task<bool> IsSubFlowCompletedAsync(
        Guid subFlowInstanceId,
        CancellationToken cancellationToken = default)
    {
        // PLACEHOLDER: This method should be implemented to check SubFlow completion
        // via a dedicated endpoint that monitors the SubFlow instance status

        // For now, return false to maintain existing behavior
        // In the actual implementation, this would make an HTTP call to check the SubFlow instance status

        await Task.CompletedTask; // Remove this when implementing the actual logic
        return false;
    }



    /// <summary>
    /// Retrieves the SubFlow workflow definition and its available transitions for the current state.
    /// This method now checks for active SubFlow correlations where the parent instance is blocked by a SubFlow.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The SubFlow workflow definition and state information if active; otherwise, null.</returns>
    public async Task<(Definitions.Workflow SubFlowWorkflow, InstanceCorrelation Correlation)?> GetActiveSubFlowContextAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var activeCorrelation = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instanceId, cancellationToken);

        // Since SubFlow now creates separate instances, we look for blocking SubFlow correlations
        // where the parent instance is the one being checked
        var subFlowCorrelation = activeCorrelation
            .FirstOrDefault(c => c.SubFlowType.Equals(SubFlowType.SubFlow) &&
                                c.ParentInstanceId == instanceId &&
                                !c.IsCompleted);

        if (subFlowCorrelation == null)
            return null;

        var subWorkflow = await componentCacheStore.GetFlowAsync(
            subFlowCorrelation.SubFlowDomain,
            subFlowCorrelation.SubFlowName,
            subFlowCorrelation.SubFlowVersion,
            cancellationToken);

        return (subWorkflow, subFlowCorrelation);
    }

    /// <summary>
    /// Checks if a workflow instance has pending sub-flows that would block transitions.
    /// This method is used to determine if a transition can be executed or should be blocked.
    /// Only SubFlow type "S" instances block the parent workflow, SubProcess type "P" instances do not.
    /// </summary>
    /// <param name="instanceId">The unique identifier of the workflow instance to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous blocking check operation.
    /// The result is true if the instance has blocking sub-flows (Type "S"); otherwise, false.
    /// </returns>
    public async Task<bool> HasBlockingSubFlowsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        return await instanceCorrelationRepository.HasActiveBlockingSubFlowsAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Handles the completion of a SubFlow by checking if the SubFlow instance has completed
    /// and updating the parent workflow accordingly.
    /// This method is now used primarily for managing SubFlow completion signals.
    /// </summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="currentSubFlowState">The current state in the SubFlow that is finishing.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous completion handling operation.</returns>
    public async Task HandleSubFlowCompletionAsync(
        Instance instance,
        State currentSubFlowState,
        CancellationToken cancellationToken = default)
    {
        // This method is now primarily used for handling completion signals
        // The actual completion checking will be done via the dedicated endpoint

        // Find active blocking SubFlow correlations for this instance
        var activeCorrelations = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instance.Id, cancellationToken);

        var blockingSubFlowCorrelations = activeCorrelations
            .Where(c => c.SubFlowType.Equals(SubFlowType.SubFlow) &&
                       c.ParentInstanceId == instance.Id &&
                       !c.IsCompleted)
            .ToList();

        // Check each blocking SubFlow for completion
        foreach (var correlation in blockingSubFlowCorrelations)
        {
            var isCompleted = await IsSubFlowCompletedAsync(correlation.SubFlowInstanceId, cancellationToken);

            if (isCompleted)
            {
                // Mark SubFlow correlation as completed
                correlation.Complete();
                await instanceCorrelationRepository.UpdateAsync(correlation, true, cancellationToken);

                // Get the parent workflow to determine the next state after SubFlow completion
                var parentWorkflow = await componentCacheStore.GetFlowAsync(
                    correlation.SubFlowDomain, // TODO: This should come from instance or correlation ??
                    instance.Flow,
                    null,
                    cancellationToken);

                // Find the parent state that initiated the SubFlow
                var parentState = parentWorkflow.GetState(correlation.ParentState);

                // Find transitions from the parent state that should be executed after SubFlow completion
                var nextTransitions = parentState.Transitions
                    .Where(t => t.TriggerType == TriggerType.Automatic)
                    .ToList();

                if (nextTransitions.Any())
                {
                    // If there are automatic transitions, execute the first one
                    var nextTransition = nextTransitions.First();
                    instance.ChangeState(nextTransition);

                    // After changing state, check if the new target state is also a SubFlow state
                    await HandleNewStateSubFlowAsync(instance, parentWorkflow, nextTransition.Target, cancellationToken);
                }
                else
                {
                    // If no automatic transitions, return to the parent state
                    instance.ChangeState(parentState);
                }
            }
        }
    }

    /// <summary>
    /// Checks if the newly transitioned state is a SubFlow state and handles it accordingly.
    /// This ensures that SubFlow chains work correctly when one SubFlow completion leads to another SubFlow.
    /// </summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="parentWorkflow">The parent workflow definition.</param>
    /// <param name="newStateKey">The key of the new state to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous new state SubFlow handling operation.</returns>
    private async Task HandleNewStateSubFlowAsync(
        Instance instance,
        Definitions.Workflow parentWorkflow,
        string newStateKey,
        CancellationToken cancellationToken = default)
    {
        var newState = parentWorkflow.GetState(newStateKey);

        // If the new state is a SubFlow state, we need to initiate the SubFlow
        if (newState.StateType == StateType.SubFlow && newState.SubFlow != null)
        {
            // Create a basic script context for SubFlow initialization
            var scriptContext = new ScriptContext.Builder()
                .SetWorkflow(parentWorkflow)
                .SetInstance(instance)
                .SetBody(new Dictionary<string, object>())
                .SetHeaders(new Dictionary<string, string>())
                .Build();

            // Handle the SubFlow for the new state
            await HandleSubFlowAsync(instance, newState, scriptContext, cancellationToken);
        }
    }
}