using BBT.Aether.Application.Services;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Scripting;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Extentions;
using BBT.Workflow.States;
using BBT.Workflow.SubFlow;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Instances;

public sealed class InstanceQueryAppService(
    IServiceProvider serviceProvider,
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository,
    IInstanceExtensionService instanceExtensionService,
    IStateMachineService stateMachineService,
    ISubFlowService subFlowService,
    IScriptContextFactory scriptContextFactory,
    IHttpContextAccessor httpContextAccessor)
    : ApplicationService(serviceProvider), IInstanceQueryAppService
{
    public async Task<InstanceServiceResult<GetInstanceOutput>> GetInstanceAsync(
        GetInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var instance = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (instance == null)
            {
                throw new EntityNotFoundException(typeof(Instance), input.Instance);
            }

            // Check ETag for conditional requests
            if (!string.IsNullOrEmpty(input.IfNoneMatch) &&
                instance.LatestData != null &&
                instance.LatestData.ETag.MatchesIfNoneMatch(input.IfNoneMatch))
            {
                return InstanceServiceResult<GetInstanceOutput>.NotModified();
            }

            var result = await BuildInstanceOutputAsync(
                input.Domain,
                input.Extension,
                input.Workflow,
                instance,
                instance.LatestData,
                ExtensionScope.GetInstance,
                cancellationToken);

            return InstanceServiceResult<GetInstanceOutput>.Success(result);
        }
    }

    public async Task<InstanceServiceResponse<PaginationResult<GetInstanceOutput>>> GetInstanceListAsync(
        GetInstanceListInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var filteredQuery = await instanceRepository.GetFilteredQueryAsync(
                input.Filter,
                cancellationToken);
 
            // Then apply pagination to the filtered query
            var pagedResult = await instanceRepository.GetPagedResultsAsync(
                input.Page,
                input.PageSize,
                input.PageUrl,
                httpContextAccessor.HttpContext?.Request.Query
                ,
                filteredQuery,
                cancellationToken
                )
 
                ;

            PaginationResult<GetInstanceOutput> result = new PaginationResult<GetInstanceOutput>()
            {
                Pagination = pagedResult.Pagination,
                Data = new List<GetInstanceOutput>()
            };
            foreach (var instance in pagedResult.Data)
            {
                var instanceOutput = await BuildInstanceOutputAsync(
                    input.Domain,
                    input.Extension,
                    input.Workflow,
                    instance,
                    instance.LatestData,
                    ExtensionScope.GetAllInstances,
                    cancellationToken);

                result.Data.Add(instanceOutput);
            }


            return new InstanceServiceResponse<PaginationResult<GetInstanceOutput>>(result);
        }
    }

    public async Task<InstanceServiceResponse<GetInstanceHistoryOutput>> GetInstanceHistoryAsync(
        GetInstanceHistoryInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var instance = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (instance == null)
            {
                throw new EntityNotFoundException(typeof(Instance), input.Instance);
            }

            var transitions = new List<GetInstanceOutput>();
            foreach (var instanceData in instance.DataList.OrderBy(d => d.EnteredAt))
            {
                var transitionOutput = await BuildInstanceOutputAsync(
                    input.Domain,
                    input.Extension,
                    input.Workflow,
                    instance,
                    instanceData,
                    ExtensionScope.GetInstance,
                    cancellationToken);

                transitions.Add(transitionOutput);
            }

            var result = new GetInstanceHistoryOutput
            {
                Transitions = transitions
            };

            return new InstanceServiceResponse<GetInstanceHistoryOutput>(result);
        }
    }

    public async Task<InstanceServiceResponse<GetAvailableTransitionOutput>> GetAvailableTransitionsAsync(
        GetAvailableTransitionInput input, CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(input.Workflow))
        {
            var instance = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);

            // Get workflow for available transitions (may have changed after transition execution)
            var workflowForTransitions = await GetCurrentWorkflowAsync(instance.Id, input.Domain, input.Workflow,
                input.Version, cancellationToken);

            // Build instance transition information using shared logic
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, workflowForTransitions, cancellationToken);

            var result = new GetAvailableTransitionOutput
            {
                Status = transitionInfo.Status,
                CurrentState = transitionInfo.CurrentState,
                Items = transitionInfo.AvailableTransitions
            };

            return new InstanceServiceResponse<GetAvailableTransitionOutput>(result);
        }
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
    /// Gets the current workflow definition based on whether the instance is in SubFlow mode or main workflow mode.
    /// </summary>
    private async Task<Definitions.Workflow> GetCurrentWorkflowAsync(
        Guid instanceId,
        string domain,
        string workflowName,
        string? version,
        CancellationToken cancellationToken = default)
    {
        var activeSubFlowContext = await subFlowService.GetActiveSubFlowContextAsync(instanceId, cancellationToken);

        if (activeSubFlowContext.HasValue)
        {
            // Instance is in SubFlow mode, use SubFlow workflow
            return activeSubFlowContext.Value.SubFlowWorkflow;
        }

        // Instance is in main workflow mode
        return await componentCacheStore.GetFlowAsync(domain, workflowName, version, cancellationToken);
    }

    /// <summary>
    /// Gets available user-triggered transitions for the current instance context (either SubFlow or main workflow).
    /// This method filters out automatic and scheduled transitions, returning only manual transitions that users can trigger.
    /// </summary>
    private async Task<List<string>> GetAvailableUserTransitionsAsync(
        Instance instance,
        Definitions.Workflow currentWorkflow,
        CancellationToken cancellationToken = default)
    {
        // Check if we're in SubFlow mode after the transition
        var activeSubFlowContext = await subFlowService.GetActiveSubFlowContextAsync(instance.Id, cancellationToken);

        if (activeSubFlowContext.HasValue)
        {
            // Verify that the current state exists in the SubFlow workflow
            // If not, the SubFlow has finished and we should use the main workflow
            var subFlowWorkflow = activeSubFlowContext.Value.SubFlowWorkflow;
            var currentStateExistsInSubFlow = subFlowWorkflow.States.Any(s => s.Key == instance.CurrentState);

            if (currentStateExistsInSubFlow)
            {
                // Return SubFlow user transitions
                return stateMachineService.AvailableUserTransitionKeys(subFlowWorkflow, instance);
            }
        }

        // Return main workflow user transitions
        return stateMachineService.AvailableUserTransitionKeys(currentWorkflow, instance);
    }
    
    private async Task<Instance> GetInstanceByIdOrKeyAsync(string instanceIdentifier,
        CancellationToken cancellationToken)
    {
        var instance = Guid.TryParse(instanceIdentifier, out var instanceId)
            ? await instanceRepository.FindAsync(instanceId, true, cancellationToken)
            : await instanceRepository.FindByKeyAsync(instanceIdentifier, cancellationToken);

        if (instance == null)
        {
            throw new EntityNotFoundException(typeof(Instance), instanceIdentifier);
        }

        return instance;
    }

    private async Task<GetInstanceOutput> BuildInstanceOutputAsync(
        string domain,
        string[]? extensionRequested,
        string workflow,
        Instance instance,
        InstanceData? instanceData,
        ExtensionScope currentScope,
        CancellationToken cancellationToken)
    {
        var flow = await componentCacheStore.GetFlowAsync(domain, workflow, null, cancellationToken);

        var response = new GetInstanceOutput
        {
            Id = instance.Id,
            Flow = instance.Flow,
            FlowVersion = instanceData?.Version ?? string.Empty,
            Etag = instanceData?.ETag ?? string.Empty,
            Domain = domain,
            Attributes = instanceData?.Data.JsonElement,
            Key = instance.Key!,
            Tags = instance.Tags,
        };

        // Process extensions for data enrichment
        var scriptContext = await scriptContextFactory.NewBuilder()
            .WithWorkflow(flow)
            .WithInstance(instance)
            .WithRuntime(runtimeInfoProvider)
            .WithBody(instanceData?.Data ?? new JsonData("{}"))
            .BuildAsync(cancellationToken);

        response.Extensions = await instanceExtensionService.ProcessExtensionsAsync(
            extensionRequested,
            scriptContext,
            flow,
            currentScope,
            cancellationToken);

        return response;
    }
}