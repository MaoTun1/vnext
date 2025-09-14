using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Remote;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Configuration;

namespace BBT.Workflow.SubFlow;

/// <summary>
/// Service for managing SubFlow and SubProcess workflows.
/// SubFlow: Runs on the main flow, preserves main flow state, acts as a wrapper.
/// SubProcess: Creates separate instances via remote calls (unchanged behavior).
/// </summary>
/// <param name="instanceCorrelationRepository">Repository for managing instance correlations.</param>
/// <param name="guidGenerator">Service for generating unique identifiers.</param>
/// <param name="remoteInstanceCommandAppService">Manages remote requests to trigger SubProcess only.</param>
/// <param name="configuration">Configuration provider for accessing application settings.</param>
/// <param name="scriptEngine">Script engine for compiling and executing mapping scripts.</param>
public sealed class SubFlowService(
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IGuidGenerator guidGenerator,
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService,
    IConfiguration configuration,
    IScriptEngine scriptEngine) : ISubFlowService
{
    /// <summary>
    /// Handles the initiation of SubFlow and SubProcess execution.
    /// SubFlow: Runs on the main flow, maintains state within the main instance.
    /// SubProcess: Creates separate instances via remote calls (unchanged behavior).
    /// </summary>
    /// <param name="workflow">The main workflow.</param>
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
        Definitions.Workflow workflow,
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

        // Both SubFlow and SubProcess now create separate instances via remote call
        // The difference is in blocking behavior handled by correlation tracking
        await CreateSubFlowInstanceAsync(workflow, parentInstance, targetState, context, cancellationToken);
    }

    /// <summary>
    /// Creates a SubFlow or SubProcess instance via remote call.
    /// Both SubFlow (Type "S") and SubProcess (Type "P") create separate instances.
    /// The difference is in blocking behavior: SubFlow blocks parent, SubProcess doesn't.
    /// Uses mapping handlers to process input and output data transformations.
    /// </summary>
    private async Task CreateSubFlowInstanceAsync(
        Definitions.Workflow workflow,
        Instance parentInstance,
        State targetState,
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
            Id = guidGenerator.Create(),
            Callback = configuration["DAPR_APP_ID"],
            Key = parentInstance.Key ?? string.Empty,
            Attributes = inputMappingResult?.Data != null
                ? JsonSerializer.SerializeToElement(inputMappingResult.Data)
                : null,
            Tags =
            [
                $"parent.key:{parentInstance.Key}",
                $"parent.domain:{workflow.Domain}",
                $"parent.flow:{workflow.Key}"
            ],
            MetaData = new ObjectDictionary
            {
                [DomainConsts.MetaDataKeys.Id] = parentInstance.Id,
                [DomainConsts.MetaDataKeys.Key] = parentInstance.Key ?? string.Empty,
                [DomainConsts.MetaDataKeys.Domain] = workflow.Domain,
                [DomainConsts.MetaDataKeys.Flow] = workflow.Key,
                [DomainConsts.MetaDataKeys.Version] = workflow.Version,
                [DomainConsts.MetaDataKeys.State] = targetState.Key
            }
        };

        // Apply additional properties from input mapping if available
        if (inputMappingResult != null)
        {
            if (!inputMappingResult.Key.IsNullOrEmpty())
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
            Convert.ToBoolean(parentInstance.MetaData[DomainConsts.MetaDataKeys.Sync]!.ToString()))
        {
            Instance = createInstanceInput,
            Headers = inputMappingResult?.Headers,
            RouteValues = inputMappingResult?.RouteValues
        };

        // Create correlation to track SubFlow/SubProcess instance
        // SubFlow (Type "S"): Blocks parent workflow until completion
        // SubProcess (Type "P"): Runs in parallel without blocking parent
        var correlation = new InstanceCorrelation(
            guidGenerator.Create(),
            parentInstance.Id,
            targetState.Key,
            createInstanceInput.Id.Value,
            subFlowConfig.Type.Code,
            subFlowConfig.Process.Domain,
            subFlowConfig.Process.Key,
            subFlowConfig.Process.Version);

        await instanceCorrelationRepository.InsertAsync(correlation, true, cancellationToken);

        await remoteInstanceCommandAppService.StartSubAsync(subFlowStartInput, cancellationToken);
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

        if (subFlowConfig.Type.Code == "P" && mappingInstance is ISubProcessMapping subProcessMapping)
        {
            return await subProcessMapping.InputHandler(context);
        }

        throw new InvalidOperationException(
            $"Failed to cast mapping instance to {mappingInterfaceType.Name} for SubFlow type '{subFlowConfig.Type.Code}'");
    }

    /// <summary>
    /// Checks if transition should be forwarded to SubFlow instance.
    /// If SubFlow is active, forwards the transition to SubFlow instance via remote call.
    /// Returns SubFlow response data if forwarded, null if should be processed locally.
    /// </summary>
    /// <param name="instanceId">The main instance ID</param>
    /// <param name="transitionKey">The transition key to execute</param>
    /// <param name="input">The transition input data</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>SubFlow transition response if forwarded, null if should be processed locally</returns>
    public async Task<InstanceServiceResponse<TransitionOutput>?> TryForwardTransitionToSubFlowAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        // Check if there's an active SubFlow that blocks the main instance
        var correlation = await instanceCorrelationRepository
            .FindActiveSubFlowByParentAsync(instanceId, cancellationToken);

        if (correlation != null)
        {
            // Forward transition to SubFlow instance
            var subFlowTransitionInput = new TransitionInput(
                correlation.SubFlowDomain,
                correlation.SubFlowName,
                correlation.SubFlowVersion)
            {
                Data = input.Data,
                Headers = input.Headers,
                RouteValues = input.RouteValues
            };

            // Forward the transition to the SubFlow instance
            var subFlowResult = await remoteInstanceCommandAppService.TransitionAsync(
                correlation.SubFlowInstanceId,
                transitionKey,
                subFlowTransitionInput,
                cancellationToken);

            return subFlowResult; // Return SubFlow response
        }

        return null; // No SubFlow active, process locally
    }
}