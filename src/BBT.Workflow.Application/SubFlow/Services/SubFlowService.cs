using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
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
/// <param name="instanceRepository">Repository for managing instance.</param>
/// <param name="guidGenerator">Service for generating unique identifiers.</param>
/// <param name="remoteInstanceCommandAppService">Manages remote requests to trigger SubProcess only.</param>
/// <param name="configuration">Configuration provider for accessing application settings.</param>
/// <param name="scriptEngine">Script engine for compiling and executing mapping scripts.</param>
public sealed class SubFlowService(
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IGuidGenerator guidGenerator,
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService,
    IConfiguration configuration,
    IScriptEngine scriptEngine,
    IInstanceRepository instanceRepository) : ISubFlowService
{
    /// <inheritdoc />
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
        var subFlowConfig = targetState.SubFlow;
        if(subFlowConfig == null)
        {
            throw new ConfigInvalidException(parentInstance.Id);
        }

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
                [DomainConsts.MetaDataKeys.State] = targetState.Key,
                [DomainConsts.MetaDataKeys.FlowType] = subFlowConfig.Type.Code
            }
        };

        // Apply additional properties from input mapping if available
        if (inputMappingResult != null)
        {
            if (!inputMappingResult.Key.IsNullOrEmpty())
            {
                createInstanceInput.Key = inputMappingResult.Key;
            }

            if (inputMappingResult.Tags?.Length > 0)
            {
                var existingTags = createInstanceInput.Tags?.ToList() ?? new List<string>();
                existingTags.AddRange(inputMappingResult.Tags);
                createInstanceInput.Tags = existingTags.ToArray();
            }
        }

        // TODO: will be removed
        // var sync = Convert.ToBoolean(parentInstance.MetaData[DomainConsts.MetaDataKeys.Sync]!.ToString());
        // if (subFlowConfig.Type.Equals(SubFlowType.SubProcess))
        // {
        //     sync = false;
        // }
        var subFlowStartInput = new StartInstanceInput(
            subFlowConfig.Process.Domain,
            subFlowConfig.Process.Key,
            subFlowConfig.Process.Version,
            sync: true
        )
        {
            Instance = createInstanceInput,
            Headers = inputMappingResult?.Headers ?? new Dictionary<string, string?>(),
            RouteValues = inputMappingResult?.RouteValues ?? new Dictionary<string, string?>()
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

        parentInstance.AddCorrelation(correlation);
        parentInstance.Busy();
        await instanceRepository.UpdateAsync(parentInstance, true, cancellationToken);

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

    /// <inheritdoc />
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

        // TODO: Remote Exception handling
        return null; // No SubFlow active, process locally
    }
}