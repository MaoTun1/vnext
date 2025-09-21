using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Aether.Guids;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Caching;
using BBT.Workflow.Instances;
using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using BBT.Workflow.Schemas;
using BBT.Workflow.States;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.SubFlow;

/// <inheritdoc cref="ISubFlowCompletionService" />
public sealed class SubFlowCompletionService(
    IServiceProvider serviceProvider,
    ICurrentSchema currentSchema,
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository,
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IScriptEngine scriptEngine,
    IScriptContextFactory scriptContextFactory,
    IRuntimeInfoProvider runtimeInfoProvider,
    IGuidGenerator guidGenerator,
    ILogger<SubFlowCompletionService> logger,
    IStateMachineExecutor stateMachineExecutor)
    : ApplicationService(serviceProvider), ISubFlowCompletionService
{
    /// <inheritdoc />
    public async Task HandleSubFlowCompletionAsync(
        FlowCompletedData completedData,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Processing SubFlow completion for instance {InstanceId} in domain {Domain}",
            completedData.InstanceId, completedData.Domain);

        // Parse parent information from metadata
        var parentInfo = ParseParentInfoFromMetaData(completedData.MetaData);
        if (parentInfo == null)
        {
            logger.LogWarning(
                "No parent information found in metadata for completed SubFlow instance {InstanceId}. This may be a standalone flow.",
                completedData.InstanceId);
            return;
        }

        // Check if this domain is handled by this runtime instance
        try
        {
            runtimeInfoProvider.Check(parentInfo.Domain);
        }
        catch (Exception)
        {
            logger.LogInformation(
                "SubFlow completion event for instance {InstanceId} belongs to domain {Domain} which is not handled by this runtime instance {RuntimeDomain}. Event will be ignored.",
                completedData.InstanceId, parentInfo.Domain, runtimeInfoProvider.Domain);
            return; // Silently ignore - this is expected in multi-domain scenarios
        }

        logger.LogDebug(
            "SubFlow completion event belongs to parent instance {ParentInstanceId} in domain {Domain}, flow {Flow}",
            parentInfo.Id, parentInfo.Domain, parentInfo.Flow);

        using (currentSchema.Change(parentInfo.Flow))
        {
            // Find the correlation record for this completed SubFlow
            var correlation = await instanceCorrelationRepository
                .FindBySubInstanceIdAsync(completedData.InstanceId, cancellationToken);

            if (correlation == null)
            {
                logger.LogWarning(
                    "No correlation found for completed SubFlow instance {InstanceId}. This may be a standalone flow or already processed.",
                    completedData.InstanceId);
                return;
            }

            if (correlation.IsCompleted)
            {
                logger.LogWarning(
                    "SubFlow correlation {CorrelationId} for instance {InstanceId} is already marked as completed.",
                    correlation.Id, completedData.InstanceId);
                return;
            }

            logger.LogInformation(
                "Found SubFlow correlation {CorrelationId} for parent instance {ParentInstanceId} in state {ParentState}",
                correlation.Id, correlation.ParentInstanceId, correlation.ParentState);

            // Mark the correlation as completed
            correlation.Complete();
            await instanceCorrelationRepository.UpdateAsync(correlation, true, cancellationToken);

            // Process parent workflow continuation within the parent's schema context
            await ProcessParentWorkflowContinuationAsync(correlation, completedData, parentInfo, cancellationToken);

            logger.LogInformation(
                "Successfully completed SubFlow processing for instance {InstanceId}",
                completedData.InstanceId);
        }
    }

    /// <summary>
    /// Processes the parent workflow continuation after SubFlow completion.
    /// This includes output mapping and resuming automatic/scheduled transitions.
    /// </summary>
    /// <param name="correlation">The SubFlow correlation</param>
    /// <param name="completedData">The completed SubFlow data</param>
    /// <param name="parentInfo">The parsed parent workflow information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ProcessParentWorkflowContinuationAsync(
        InstanceCorrelation correlation,
        FlowCompletedData completedData,
        ParentInfo parentInfo,
        CancellationToken cancellationToken)
    {
        // Get the parent instance
        var parentInstance = await instanceRepository.GetActiveAsync(correlation.ParentInstanceId, cancellationToken);

        logger.LogInformation(
            "Processing parent workflow continuation for instance {ParentInstanceId} in state {CurrentState}",
            parentInstance.Id, parentInstance.CurrentState);

        // Get parent workflow definition
        var parentWorkflow = await componentCacheStore.GetFlowAsync(
            parentInfo.Domain,
            parentInfo.Flow,
            parentInfo.Version, // Use the specific version from parent info
            cancellationToken);

        // Get the state where SubFlow was initiated
        var parentState = parentWorkflow.GetState(correlation.ParentState);

        // Create script context for output mapping with completed SubFlow data
        var scriptContextBuilder = scriptContextFactory.NewBuilder()
            .WithWorkflow(parentWorkflow)
            .WithInstance(parentInstance)
            .WithRuntime(runtimeInfoProvider)
            .WithBody(completedData.InstanceData?.Deserialize<Dictionary<string, object>>() ??
                      new Dictionary<string, object>());
        
        if (parentState.SubFlow?.Mapping != null)
        {
            logger.LogInformation(
                "Processing SubFlow output mapping for parent instance {ParentInstanceId}",
                parentInstance.Id);

            await ProcessSubFlowOutputMappingAsync(
                parentWorkflow,
                parentInstance,
                parentState,
                completedData,
                await scriptContextBuilder.BuildAsync(cancellationToken),
                cancellationToken);
        }

        // Resume automatic transitions and scheduled processes that were paused for SubFlow
        logger.LogInformation(
            "Resuming automatic transitions for parent instance {ParentInstanceId} after SubFlow completion",
            parentInstance.Id);
        

        await ResumeAutomaticProcessesAsync(
            parentInstance, 
            parentWorkflow,
            await scriptContextBuilder.BuildAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Processes SubFlow output mapping by executing the mapping script and merging results into parent instance data.
    /// </summary>
    /// <param name="parentWorkflow">The parent workflow.</param>
    /// <param name="parentInstance">The parent workflow instance</param>
    /// <param name="parentState">The parent state containing SubFlow configuration</param>
    /// <param name="completedData">The completed SubFlow data</param>
    /// <param name="scriptContext"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ProcessSubFlowOutputMappingAsync(
        Definitions.Workflow parentWorkflow,
        Instance parentInstance,
        Definitions.State parentState,
        FlowCompletedData completedData,
        ScriptContext scriptContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var subFlowConfig = parentState.SubFlow!;
            var mappingCode = subFlowConfig.Mapping!.DecodedCode;

            logger.LogDebug(
                "Executing SubFlow output mapping for parent instance {ParentInstanceId}",
                parentInstance.Id);
            
            // Compile the mapping script to the appropriate interface
            var mappingInstance = await scriptEngine.CompileToInstanceAsync<object>(
                mappingCode,
                cancellationToken: cancellationToken);

            ScriptResponse? outputMappingResult = null;

            // Execute OutputHandler for SubFlow (SubProcess doesn't have OutputHandler)
            if (subFlowConfig.Type.Code == "S" && mappingInstance is ISubFlowMapping subFlowMapping)
            {
                outputMappingResult = await subFlowMapping.OutputHandler(scriptContext);
            }
            else if (subFlowConfig.Type.Code == "P")
            {
                logger.LogInformation(
                    "SubProcess type 'P' does not support output mapping - skipping output processing for parent instance {ParentInstanceId}",
                    parentInstance.Id);
                // SubProcess instances don't have output handling - they run independently
                return;
            }

            if (outputMappingResult?.Data != null)
            {
                logger.LogInformation(
                    "Merging SubFlow output data into parent instance {ParentInstanceId}",
                    parentInstance.Id);

                // Add the mapped output data to the parent instance
                parentInstance.AddData(
                    guidGenerator.Create(),
                    new JsonData(JsonSerializer.Serialize(outputMappingResult.Data)),
                    parentState.VersionStrategy);

                await instanceRepository.UpdateAsync(parentInstance, true, cancellationToken);

                logger.LogDebug(
                    "Successfully merged SubFlow output data into parent instance {ParentInstanceId}",
                    parentInstance.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process SubFlow output mapping for parent instance {ParentInstanceId}",
                parentInstance.Id);

            // Don't throw - we still want to resume automatic processes even if mapping fails
        }
    }

    /// <summary>
    /// Resumes automatic transitions and scheduled processes for the parent workflow after SubFlow completion.
    /// This continues the workflow execution that was paused while waiting for SubFlow completion.
    /// </summary>
    /// <param name="parentInstance">The parent workflow instance</param>
    /// <param name="parentWorkflow">The parent workflow definition</param>
    /// <param name="scriptContext"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ResumeAutomaticProcessesAsync(
        Instance parentInstance,
        Definitions.Workflow parentWorkflow,
        ScriptContext scriptContext,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Resuming automatic processes for parent instance {ParentInstanceId} in state {CurrentState}",
                parentInstance.Id, parentInstance.CurrentState);
            
            await stateMachineExecutor.CheckAndExecuteAutomaticTransitionsAsync(parentWorkflow, parentInstance,
                cancellationToken);
            
            await stateMachineExecutor.ScheduleTransitionsForLaterExecutionAsync(
                parentWorkflow, 
                parentInstance,
                scriptContext,
                cancellationToken);
            
            logger.LogInformation(
                "Successfully resumed automatic processes for parent instance {ParentInstanceId}",
                parentInstance.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to resume automatic processes for parent instance {ParentInstanceId}",
                parentInstance.Id);

            // Don't throw - SubFlow completion should still be marked as successful
        }
    }

    /// <summary>
    /// Parses parent workflow information from the SubFlow instance metadata.
    /// MetaData is expected to contain keys with "parent." prefix.
    /// </summary>
    /// <param name="metaData">The metadata dictionary from the completed SubFlow instance</param>
    /// <returns>Parsed parent information or null if not found</returns>
    private ParentInfo? ParseParentInfoFromMetaData(ObjectDictionary? metaData)
    {
        if (metaData == null || metaData.Count == 0)
        {
            return null;
        }

        var parentInfo = new ParentInfo();
        var foundAnyParentData = false;

        foreach (var kvp in metaData)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            var stringValue = value?.ToString();

            if (string.IsNullOrEmpty(stringValue))
                continue;

            switch (key)
            {
                case DomainConsts.MetaDataKeys.Id:
                    if (Guid.TryParse(stringValue, out var parentId))
                    {
                        parentInfo.Id = parentId;
                        foundAnyParentData = true;
                    }
                    break;
                case DomainConsts.MetaDataKeys.Key:
                    parentInfo.Key = stringValue;
                    foundAnyParentData = true;
                    break;
                case DomainConsts.MetaDataKeys.Domain:
                    parentInfo.Domain = stringValue;
                    foundAnyParentData = true;
                    break;
                case DomainConsts.MetaDataKeys.Flow:
                    parentInfo.Flow = stringValue;
                    foundAnyParentData = true;
                    break;
                case DomainConsts.MetaDataKeys.Version:
                    parentInfo.Version = stringValue;
                    foundAnyParentData = true;
                    break;
                case DomainConsts.MetaDataKeys.State:
                    parentInfo.State = stringValue;
                    foundAnyParentData = true;
                    break;
            }
        }

        if (!foundAnyParentData || string.IsNullOrEmpty(parentInfo.Domain) || string.IsNullOrEmpty(parentInfo.Flow))
        {
            logger.LogWarning(
                "Incomplete parent information found in metadata. Domain: {Domain}, Flow: {Flow}",
                parentInfo.Domain ?? "null", parentInfo.Flow ?? "null");
            return null;
        }

        return parentInfo;
    }

    /// <summary>
    /// Contains parsed parent workflow information from SubFlow metadata
    /// </summary>
    private class ParentInfo
    {
        public Guid Id { get; set; }
        public string? Key { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string Flow { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? State { get; set; }
    }
}