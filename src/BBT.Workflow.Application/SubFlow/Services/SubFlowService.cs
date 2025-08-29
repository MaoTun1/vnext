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
/// SubFlow: Runs on the main flow, preserves main flow state, acts as a wrapper.
/// SubProcess: Creates separate instances via remote calls (unchanged behavior).
/// </summary>
/// <param name="componentCacheStore">Cache store for retrieving workflow definitions.</param>
/// <param name="instanceCorrelationRepository">Repository for managing instance correlations.</param>
/// <param name="guidGenerator">Service for generating unique identifiers.</param>
/// <param name="remoteInstanceCommandAppService">Manages remote requests to trigger SubProcess only.</param>
/// <param name="scriptEngine">Script engine for compiling and executing mapping scripts.</param>
public sealed class SubFlowService(
    IComponentCacheStore componentCacheStore,
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IGuidGenerator guidGenerator,
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService,
    IScriptEngine scriptEngine) : ISubFlowService
{
    /// <summary>
    /// Handles the initiation of SubFlow and SubProcess execution.
    /// SubFlow: Runs on the main flow, maintains state within the main instance.
    /// SubProcess: Creates separate instances via remote calls (unchanged behavior).
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
    /// SubFlow (Type: "S"): Runs on the main flow instance, preserves main flow state. 
    /// Main flow wraps SubFlow, tracks which SubFlow is started, and routes transition requests to SubFlow.
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

        // Handle based on SubFlow type
        if (subFlowConfig.Type.Code == "S")
        {
            // SubFlow: Run on main instance, preserve main flow state
            await HandleSubFlowOnMainInstanceAsync(parentInstance, targetState, subWorkflow, context, cancellationToken);
        }
        else if (subFlowConfig.Type.Code == "P")
        {
            // SubProcess: Create separate instance via remote call (unchanged behavior)
            await HandleSubProcessWithRemoteCallAsync(parentInstance, targetState, subWorkflow, context, cancellationToken);
        }
    }

    /// <summary>
    /// Handles SubFlow execution on the main instance.
    /// SubFlow runs on the main flow instance, preserves main flow state.
    /// Creates correlation to track SubFlow execution.
    /// </summary>
    private async Task HandleSubFlowOnMainInstanceAsync(
        Instance parentInstance,
        State targetState,
        Definitions.Workflow subWorkflow,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var subFlowConfig = targetState.SubFlow!;

        // Handle input mapping if mapping is configured
        ScriptResponse? inputMappingResult = null;
        if (subFlowConfig.Mapping != null)
        {
            inputMappingResult = await HandleInputMappingAsync(subFlowConfig, context, cancellationToken);
        }

        // Create correlation to track SubFlow on main instance
        var correlation = new InstanceCorrelation(
            guidGenerator.Create(),
            parentInstance.Id,
            targetState.Key,
            parentInstance.Id, // SubFlow runs on the same instance
            subFlowConfig.Type.Code,
            subFlowConfig.Process.Domain,
            subFlowConfig.Process.Key,
            subFlowConfig.Process.Version);

        await instanceCorrelationRepository.InsertAsync(correlation, true, cancellationToken);

        // Set instance to SubFlow's initial state
        var initialSubFlowState = subWorkflow.GetInitialState();
        parentInstance.ChangeState(initialSubFlowState);

        // Add SubFlow instance data if input mapping provided data
        if (inputMappingResult?.Data != null)
        {
            var jsonData = new JsonData(inputMappingResult.Data);
            parentInstance.AddData(
                guidGenerator.Create(),
                jsonData,
                VersionStrategy.IncreaseMinor);
        }
    }

    /// <summary>
    /// Handles SubProcess execution by creating separate instances via remote calls.
    /// SubProcess runs in parallel without blocking the parent workflow.
    /// Uses mapping handlers to process input and output data transformations.
    /// </summary>
    private async Task HandleSubProcessWithRemoteCallAsync(
        Instance parentInstance,
        State targetState,
        Definitions.Workflow subWorkflow,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var subFlowConfig = targetState.SubFlow!;

        // Handle input mapping if mapping is configured
        ScriptResponse? inputMappingResult = null;
        if (subFlowConfig.Mapping != null)
        {
            inputMappingResult = await HandleInputMappingAsync(subFlowConfig, context, cancellationToken);
        }

        // Prepare instance creation input
        var createInstanceInput = new CreateInstanceInput
        {
            Key = parentInstance.Key ?? string.Empty,
            Attributes = inputMappingResult?.Data,
            Tags = 
            [
                $"parent:{parentInstance.Id}",
                $"parent-state:{targetState.Key}",
                $"type:{subFlowConfig.Type.Code}"
            ]
        };

        // Apply additional properties from input mapping if available
        if (inputMappingResult != null)
        {
            if (inputMappingResult.Key != null)
            {
                createInstanceInput.Key = inputMappingResult.Key;
            }

            if (inputMappingResult.Tags != null && inputMappingResult.Tags.Length > 0)
            {
                var existingTags = createInstanceInput.Tags?.ToList() ?? new List<string>();
                existingTags.AddRange(inputMappingResult.Tags);
                createInstanceInput.Tags = existingTags.ToArray();
            }
        }

        var subFlowStartInput = new StartInstanceInput(
            subFlowConfig.Process.Domain,
            subFlowConfig.Process.Key,
            subFlowConfig.Process.Version,
            false)
        {
            Instance = createInstanceInput,
            Headers = inputMappingResult?.Headers,
            RouteValues = inputMappingResult?.RouteValues
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
    /// Gets the SubFlow workflow definition and current state for SubFlow running on main instance.
    /// This method returns the SubFlow workflow and correlation when a SubFlow is active.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The SubFlow workflow definition and correlation if active; otherwise, null.</returns>
    public async Task<(Definitions.Workflow SubFlowWorkflow, InstanceCorrelation Correlation)?>  
        GetActiveSubFlowOnMainInstanceAsync(
            Guid instanceId,
            CancellationToken cancellationToken = default)
    {
        var activeCorrelations = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instanceId, cancellationToken);

        // Look for SubFlow correlations where SubFlow runs on the main instance
        var subFlowCorrelation = activeCorrelations
            .FirstOrDefault(c => c.SubFlowType.Equals(SubFlowType.SubFlow) &&
                                 c.ParentInstanceId == instanceId &&
                                 c.SubFlowInstanceId == instanceId && // SubFlow runs on main instance
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
    /// Handles input mapping for SubFlow/SubProcess by compiling and executing the mapping script.
    /// </summary>
    /// <param name="subFlowConfig">The SubFlow configuration containing mapping information.</param>
    /// <param name="context">The script context for mapping execution.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The result of the input mapping handler.</returns>
    private async Task<ScriptResponse?> HandleInputMappingAsync(
        Definitions.SubFlow subFlowConfig,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var mappingCode = subFlowConfig.Mapping.DecodedCode;

        // Determine the appropriate mapping interface based on SubFlow type
        var mappingInterfaceType = subFlowConfig.Type.Code == "S"
            ? typeof(ISubFlowMapping)
            : typeof(ISubProcessMapping);

        // Compile the mapping script to the appropriate interface
        var mappingInstance = await scriptEngine.CompileToInstanceAsync<object>(
            mappingCode,
            cancellationToken: cancellationToken);

        // Cast to the appropriate mapping interface and execute InputHandler
        if (subFlowConfig.Type.Code == "S" && mappingInstance is ISubFlowMapping subFlowMapping)
        {
            return await subFlowMapping.InputHandler(context);
        }
        else if (subFlowConfig.Type.Code == "P" && mappingInstance is ISubProcessMapping subProcessMapping)
        {
            return await subProcessMapping.InputHandler(context);
        }

        throw new InvalidOperationException(
            $"Failed to cast mapping instance to {mappingInterfaceType.Name} for SubFlow type '{subFlowConfig.Type.Code}'");
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
    /// This method checks for active SubFlow correlations.
    /// For SubFlow (Type "S"): Returns SubFlow running on main instance.
    /// For SubProcess (Type "P"): Returns blocking SubFlow correlations (unchanged behavior).
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The SubFlow workflow definition and state information if active; otherwise, null.</returns>
    public async Task<(Definitions.Workflow SubFlowWorkflow, InstanceCorrelation Correlation)?>
        GetActiveSubFlowContextAsync(
            Guid instanceId,
            CancellationToken cancellationToken = default)
    {
        // First check for SubFlow running on main instance
        var subFlowOnMainInstance = await GetActiveSubFlowOnMainInstanceAsync(instanceId, cancellationToken);
        if (subFlowOnMainInstance.HasValue)
        {
            return subFlowOnMainInstance.Value;
        }

        // Then check for SubProcess blocking correlations (unchanged behavior)
        var activeCorrelation = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instanceId, cancellationToken);

        var subProcessCorrelation = activeCorrelation
            .FirstOrDefault(c => c.SubFlowType.Equals(SubFlowType.SubFlow) &&
                                 c.ParentInstanceId == instanceId &&
                                 c.SubFlowInstanceId != instanceId && // SubProcess has separate instance
                                 !c.IsCompleted);

        if (subProcessCorrelation == null)
            return null;

        var subWorkflow = await componentCacheStore.GetFlowAsync(
            subProcessCorrelation.SubFlowDomain,
            subProcessCorrelation.SubFlowName,
            subProcessCorrelation.SubFlowVersion,
            cancellationToken);

        return (subWorkflow, subProcessCorrelation);
    }

    /// <summary>
    /// Checks if a workflow instance has pending sub-flows that would block transitions.
    /// SubFlow (Type "S"): Running on main instance - check for active correlations.
    /// SubProcess (Type "P"): Running on separate instances - does not block main instance.
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
        // Check if there are any active SubFlow correlations (Type "S") for this instance
        var activeCorrelations = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instanceId, cancellationToken);

        // SubFlow (Type "S") running on main instance blocks transitions
        var hasBlockingSubFlow = activeCorrelations.Any(c => 
            c.SubFlowType.Equals(SubFlowType.SubFlow) &&
            c.ParentInstanceId == instanceId &&
            c.SubFlowInstanceId == instanceId && // SubFlow runs on main instance
            !c.IsCompleted);

        return hasBlockingSubFlow;
    }

    /// <summary>
    /// Handles the completion of a SubFlow running on the main instance.
    /// When SubFlow completes, returns all instance data to main flow and executes output mapping.
    /// Main flow then continues with automatic transitions.
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
        // Find active SubFlow correlations for this instance
        var activeCorrelations = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instance.Id, cancellationToken);

        // Handle SubFlow completion (Type "S" - running on main instance)
        var subFlowCorrelations = activeCorrelations
            .Where(c => c.SubFlowType.Equals(SubFlowType.SubFlow) &&
                        c.ParentInstanceId == instance.Id &&
                        c.SubFlowInstanceId == instance.Id && // SubFlow runs on main instance
                        !c.IsCompleted)
            .ToList();

        foreach (var correlation in subFlowCorrelations)
        {
            // Mark SubFlow correlation as completed
            correlation.Complete();
            await instanceCorrelationRepository.UpdateAsync(correlation, true, cancellationToken);

            // Get the parent workflow to determine the next state after SubFlow completion
            var parentWorkflow = await componentCacheStore.GetFlowAsync(
                correlation.SubFlowDomain,
                instance.Flow,
                null,
                cancellationToken);

            // Find the parent state that initiated the SubFlow
            var parentState = parentWorkflow.GetState(correlation.ParentState);

            // Handle output mapping if the parent state has SubFlow configuration with mapping
            if (parentState.SubFlow?.Mapping != null && parentState.SubFlow.Type.Code == "S")
            {
                await HandleOutputMappingAsync(instance, parentState.SubFlow, cancellationToken);
            }

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
                await HandleNewStateSubFlowAsync(instance, parentWorkflow, nextTransition.Target,
                    cancellationToken);
            }
            else
            {
                // If no automatic transitions, return to the parent state
                instance.ChangeState(parentState);
            }
        }

        // Also handle SubProcess completion (Type "P" - separate instances) - unchanged behavior
        var subProcessCorrelations = activeCorrelations
            .Where(c => c.SubFlowType.Equals(SubFlowType.SubFlow) &&
                        c.ParentInstanceId == instance.Id &&
                        c.SubFlowInstanceId != instance.Id && // SubProcess has separate instance
                        !c.IsCompleted)
            .ToList();

        foreach (var correlation in subProcessCorrelations)
        {
            var isCompleted = await IsSubFlowCompletedAsync(correlation.SubFlowInstanceId, cancellationToken);

            if (isCompleted)
            {
                // Mark SubProcess correlation as completed
                correlation.Complete();
                await instanceCorrelationRepository.UpdateAsync(correlation, true, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Handles output mapping for SubFlow completion by compiling and executing the mapping script.
    /// Only applicable for SubFlow type "S" which has both InputHandler and OutputHandler.
    /// SubFlow returns all instance data to main flow, and main flow merges it via output mapping.
    /// </summary>
    /// <param name="instance">The parent workflow instance.</param>
    /// <param name="subFlowConfig">The SubFlow configuration containing mapping information.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous output mapping operation.</returns>
    private async Task HandleOutputMappingAsync(
        Instance instance,
        Definitions.SubFlow subFlowConfig,
        CancellationToken cancellationToken = default)
    {
        var mappingCode = subFlowConfig.Mapping.DecodedCode;

        // Compile the mapping script to ISubFlowMapping interface
        var mappingInstance = await scriptEngine.CompileToInstanceAsync<ISubFlowMapping>(
            mappingCode,
            null,
            null,
            cancellationToken);

        // Create a script context for output mapping
        // Get the correlation to access SubFlow domain information
        var correlations = await instanceCorrelationRepository
            .FindActiveByParentInstanceIdAsync(instance.Id, cancellationToken);
        var subFlowCorrelation = correlations.FirstOrDefault(c => 
            c.ParentInstanceId == instance.Id && 
            c.SubFlowInstanceId == instance.Id && 
            c.SubFlowType.Equals(SubFlowType.SubFlow));

        if (subFlowCorrelation == null)
        {
            throw new InvalidOperationException($"No SubFlow correlation found for instance {instance.Id}");
        }

        var subFlowWorkflow = await componentCacheStore.GetFlowAsync(
            subFlowCorrelation.SubFlowDomain,
            subFlowCorrelation.SubFlowName,
            subFlowCorrelation.SubFlowVersion,
            cancellationToken);

        var scriptContext = new ScriptContext.Builder()
            .SetWorkflow(subFlowWorkflow)
            .SetInstance(instance)
            .SetBody(instance.Data) // SubFlow's complete instance data
            .SetHeaders(new Dictionary<string, string>())
            .Build();

        // Execute the OutputHandler
        var outputMappingResult = await mappingInstance.OutputHandler(scriptContext);

        // Apply the output mapping result to the instance
        if (outputMappingResult?.Data != null)
        {
            // Merge the output data into the existing instance attributes
            var jsonData = new JsonData(outputMappingResult.Data);
            instance.AddData(
                guidGenerator.Create(),
                jsonData,
                VersionStrategy.IncreaseMinor);
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