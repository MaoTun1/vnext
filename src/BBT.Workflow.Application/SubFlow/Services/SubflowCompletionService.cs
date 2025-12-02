using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Aether.Guids;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;
using BBT.Workflow.Execution.Pipeline;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Shared;

namespace BBT.Workflow.SubFlow;

/// <inheritdoc cref="ISubflowCompletionService" />
public sealed class SubflowCompletionService(
    IServiceProvider serviceProvider,
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository,
    IScriptEngine scriptEngine,
    IScriptContextFactory scriptContextFactory,
    IRuntimeInfoProvider runtimeInfoProvider,
    IGuidGenerator guidGenerator,
    IWorkflowExecutionService workflowExecutionService,
    ILogger<SubflowCompletionService> logger)
    : ApplicationService(serviceProvider), ISubflowCompletionService
{
    /// <inheritdoc />
    public async Task CompletionAsync(
        FlowCompletedInput completedInput,
        CancellationToken cancellationToken = default)
    {
        // Check if this domain is handled by this runtime instance
        try
        {
            runtimeInfoProvider.Check(completedInput.Domain);
        }
        catch (NotFoundDomainException)
        {
            // Silently ignore - this is expected in multi-domain scenarios
            return;
        }

        try
        {
            // Get parent instance
            var parentInstance = await instanceRepository.FindAsync(
                completedInput.InstanceId, true, cancellationToken);

            if (parentInstance == null)
            {
                logger.InstanceNotFound(
                    completedInput.InstanceId,
                    completedInput.Flow);
                return;
            }

            // Complete correlation through Instance aggregate
            var correlation = parentInstance.CompleteCorrelation(completedInput.SubInstanceId);
            if (correlation == null)
            {
                logger.SubFlowCorrelationNotFound(completedInput.SubInstanceId);
                return;
            }

            logger.SubFlowCorrelationCompleted(
                completedInput.SubInstanceId,
                completedInput.InstanceId);

            // Save instance (correlation is part of aggregate)
            await instanceRepository.UpdateAsync(parentInstance, true, cancellationToken);

            // If this is a SubProcess (non-blocking), just return after marking correlation as completed
            if (correlation.SubFlowType.Equals(SubFlowType.SubProcess))
            {
                return;
            }

            // Process parent workflow continuation for SubFlow (blocking)
            await ProcessParentWorkflowContinuationAsync(
                correlation,
                completedInput,
                parentInstance,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.SubFlowCompletionFailed(
                ex,
                completedInput.SubInstanceId,
                completedInput.InstanceId);
        }
    }

    /// <summary>
    /// Processes the parent workflow continuation after SubFlow completion.
    /// This includes output mapping and resuming automatic/scheduled transitions.
    /// </summary>
    /// <param name="correlation">The SubFlow correlation</param>
    /// <param name="completedInput">The completed SubFlow data</param>
    /// <param name="parentInstance">The parent instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ProcessParentWorkflowContinuationAsync(
        InstanceCorrelation correlation,
        FlowCompletedInput completedInput,
        Instance parentInstance,
        CancellationToken cancellationToken)
    {
        logger.SubFlowParentContinuationStarted(
            parentInstance.Id,
            parentInstance.GetCurrentState);

        // Get parent workflow definition using data from completedInput
        var parentWorkflowResult = await componentCacheStore.GetFlowAsync(
            completedInput.Domain,
            completedInput.Flow,
            completedInput.Version,
            cancellationToken);

        if (!parentWorkflowResult.IsSuccess)
        {
            logger.LogWarning("Failed to get parent workflow {Flow} for SubFlow completion: {ErrorCode}",
                completedInput.Flow, parentWorkflowResult.Error.Code);
            return;
        }

        var parentWorkflow = parentWorkflowResult.Value!;

        // Get the state where SubFlow was initiated using Result Pattern
        var parentStateResult = parentWorkflow.GetState(correlation.ParentState);
        if (!parentStateResult.IsSuccess)
        {
            // Log error using WorkflowErrors - state not found
            return;
        }

        var parentState = parentStateResult.Value!;

        // Create script context for output mapping with completed SubFlow data
        var scriptContextBuilder = scriptContextFactory.NewBuilder()
            .WithWorkflow(parentWorkflow)
            .WithInstance(parentInstance)
            .WithRuntime(runtimeInfoProvider)
            .WithBody(completedInput.InstanceData?.Deserialize<Dictionary<string, object>>() ??
                      new Dictionary<string, object>());

        if (parentState.SubFlow?.Mapping != null)
        {
            logger.SubFlowOutputMappingStarted(parentInstance.Id);

            await ProcessSubFlowOutputMappingAsync(
                parentInstance,
                parentState,
                await scriptContextBuilder.BuildAsync(cancellationToken),
                cancellationToken);
        }

        // Resume automatic transitions and scheduled processes that were paused for SubFlow
        logger.SubFlowPipelineResumed(parentInstance.Id);

        await ResumePipelineAsync(
            parentInstance,
            parentWorkflow,
            cancellationToken);
    }

    /// <summary>
    /// Processes SubFlow output mapping by executing the mapping script and merging results into parent instance data.
    /// </summary>
    /// <param name="parentInstance">The parent workflow instance</param>
    /// <param name="parentState">The parent state containing SubFlow configuration</param>
    /// <param name="scriptContext"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ProcessSubFlowOutputMappingAsync(
        Instance parentInstance,
        State parentState,
        ScriptContext scriptContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var subFlowConfig = parentState.SubFlow;
            if (subFlowConfig == null)
            {
                return;
            }

            var mappingCode = subFlowConfig.Mapping.DecodedCode;

            // Compile the mapping script to the appropriate interface
            var mappingInstance = await scriptEngine.CompileToInstanceAsync<object>(
                mappingCode,
                cancellationToken: cancellationToken);

            ScriptResponse? outputMappingResult = null;

            // Execute OutputHandler for SubFlow (SubProcess doesn't have OutputHandler)
            if (subFlowConfig.Type.Equals(SubFlowType.SubFlow) && mappingInstance is ISubFlowMapping subFlowMapping)
            {
                outputMappingResult = await subFlowMapping.OutputHandler(scriptContext);
            }

            if (outputMappingResult?.Data != null)
            {
                // Add the mapped output data to the parent instance
                parentInstance.AddData(
                    guidGenerator.Create(),
                    new JsonData(JsonSerializer.Serialize(outputMappingResult.Data)),
                    parentState.VersionStrategy);

                await instanceRepository.UpdateAsync(parentInstance, true, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.SubFlowCompletionFailed(
                ex,
                Guid.Empty,
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
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ResumePipelineAsync(
        Instance parentInstance,
        Definitions.Workflow parentWorkflow,
        CancellationToken cancellationToken)
    {
        try
        {
            var input = new WorkflowExecutionContext
            {
                Domain = parentWorkflow.Domain,
                WorkflowKey = parentWorkflow.Key,
                WorkflowVersion = parentWorkflow.Version,
                InstanceId = parentInstance.Id.ToString(),
                TransitionKey = "", // For logging purposes only
                TriggerType = TriggerType.Manual,
                Mode = ExecMode.Resume, // Use Resume mode for SubFlow completion
                Headers = new Dictionary<string, string?>(),
                Actor = ExecutionActor.System,
                RequestedAt = DateTimeOffset.UtcNow,
                Execution = new ExecutionInfo
                {
                    ExecutionChainId = Guid.NewGuid().ToString("N"),
                    ChainDepth = 0,
                    ResumeFrom = LifecycleOrder.ClearBusyOnResumeStep,
                    IsSubFlowResume = true
                }
            };

            var result = await workflowExecutionService.ExecuteTransitionAsync(input, cancellationToken);

            if (!result.IsSuccess)
            {
                // Check if this is an auto-transition condition not met
                if (result.Error.Code != WorkflowErrorCodes.AutoTransitionConditionNotMet)
                {
                    logger.TransitionRuleFailed(
                        "subflow",
                        parentInstance.Id,
                        result.Error.Message ?? "Unknown error");
                }
            }
        }
        catch (Exception ex)
        {
            logger.SubFlowCompletionFailed(
                ex,
                Guid.Empty,
                parentInstance.Id);

            // Don't throw - SubFlow completion should still be marked as successful
        }
    }
}