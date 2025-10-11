using System.Text.Json;
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
/// <param name="remoteInstanceCommandAppService">Manages remote requests to trigger SubProcess only.</param>
/// <param name="configuration">Configuration provider for accessing application settings.</param>
/// <param name="scriptEngine">Script engine for compiling and executing mapping scripts.</param>
public sealed class SubflowStarter(
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService,
    IConfiguration configuration,
    IScriptEngine scriptEngine) : ISubflowStarter
{
    /// <inheritdoc />
    public async Task StartAsync(
        Definitions.Workflow workflow,
        Instance parentInstance,
        State targetState,
        Transition transition,
        InstanceCorrelation correlation,
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
            Id = correlation.SubFlowInstanceId,
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
                [DomainConsts.MetaDataKeys.Transition] = transition.Key,
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

            if (!inputMappingResult.Tags.IsNullOrEmpty())
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
            sync: true
        )
        {
            Instance = createInstanceInput,
            Headers = inputMappingResult?.Headers ?? new Dictionary<string, string?>(),
            RouteValues = inputMappingResult?.RouteValues ?? new Dictionary<string, string?>()
        };

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
}