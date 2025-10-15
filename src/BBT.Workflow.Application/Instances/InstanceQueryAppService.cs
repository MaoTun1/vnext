using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Scripting;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Extentions;
using BBT.Workflow.States;
using Microsoft.AspNetCore.Http;
using BBT.Workflow.Instances.Remote;
using Microsoft.Extensions.Logging;
using BBT.Workflow.Tasks.Extensions;
using BBT.Workflow.Shared;

namespace BBT.Workflow.Instances;

public sealed class InstanceQueryAppService(
    IServiceProvider serviceProvider,
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository,
    IInstanceCorrelationRepository instanceCorrelationRepository,
    IInstanceExtensionService instanceExtensionService,
    IStateMachineService stateMachineService,
    IScriptContextFactory scriptContextFactory,
    IHttpContextAccessor httpContextAccessor,
    IRemoteInstanceQueryAppService remoteInstanceQueryAppService,
    ILogger<InstanceQueryAppService> logger)
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
            var currentWorkflow = await componentCacheStore.GetFlowAsync(
                input.Domain,
                input.Workflow,
                input.Version,
                cancellationToken);

            // Build instance transition information using shared logic
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

            // Check if there are any active SubFlow correlations
            var activeSubFlowCorrelation = transitionInfo.ActiveCorrelations
                .Where(c => c.SubFlowType == SubFlowType.SubFlow && !c.IsCompleted)
                .OrderByDescending(c => c.CorrelationId) // Get the latest active SubFlow
                .FirstOrDefault();

            (List<string> availableTransitions, string? currentState, InstanceStatus? status) = activeSubFlowCorrelation != null
                ? await GetSubFlowTransitionsAsync(activeSubFlowCorrelation, instance, currentWorkflow, cancellationToken)
                : GetMainFlowTransitions(instance, currentWorkflow, transitionInfo);

            var result = new GetAvailableTransitionOutput
            {
                Status = status,
                CurrentState = currentState,
                Items = availableTransitions,
                ActiveCorrelations = transitionInfo.ActiveCorrelations
            };

            return new InstanceServiceResponse<GetAvailableTransitionOutput>(result);
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
        Task<(InstanceStatus Status, string? CurrentState, List<InstanceCorrelationInfo>
            ActiveCorrelations)> BuildInstanceTransitionInfoAsync(
            Instance instance,
            CancellationToken cancellationToken = default)
    {
        // Get active correlations
        var activeCorrelations = await GetActiveCorrelationsAsync(instance.Id, cancellationToken);

        return (instance.Status, instance.CurrentState, activeCorrelations);
    }

    /// <summary>
    /// Gets available transitions from a remote SubFlow instance.
    /// </summary>
    /// <param name="activeSubFlowCorrelation">The active SubFlow correlation</param>
    /// <param name="mainInstance">The main workflow instance</param>
    /// <param name="currentWorkflow">The current workflow definition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing available transitions and current state from SubFlow, status always from main flow</returns>
    private async Task<(List<string> AvailableTransitions, string? CurrentState, InstanceStatus? Status)> GetSubFlowTransitionsAsync(
        InstanceCorrelationInfo activeSubFlowCorrelation,
        Instance mainInstance,
        BBT.Workflow.Definitions.Workflow currentWorkflow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subFlowInput = new GetAvailableTransitionInput
            {
                Domain = activeSubFlowCorrelation.SubFlowDomain,
                Workflow = activeSubFlowCorrelation.SubFlowName,
                Version = activeSubFlowCorrelation.SubFlowVersion,
                Instance = activeSubFlowCorrelation.SubFlowInstanceId.ToString()
            };

            var subFlowResult = await remoteInstanceQueryAppService.GetAvailableTransitionsAsync(
                subFlowInput,
                cancellationToken);

            if (subFlowResult?.Data != null)
            {
                // Use SubFlow transitions and current state, but always use main instance status
                return (subFlowResult.Data.Items, subFlowResult.Data.CurrentState, mainInstance.Status);
            }
        }
        catch (Exception ex)
        {
            // Log the exception and fall back to main flow transitions
            logger.LogWarning(ex,
                "Failed to get available transitions from SubFlow {SubFlowDomain}/{SubFlowName} for instance {InstanceId}. Falling back to main flow transitions.",
                activeSubFlowCorrelation.SubFlowDomain,
                activeSubFlowCorrelation.SubFlowName,
                activeSubFlowCorrelation.SubFlowInstanceId);
        }

        // Fallback to main flow transitions
        return GetMainFlowTransitions(mainInstance, currentWorkflow);
    }

    /// <summary>
    /// Gets available transitions from the main workflow instance.
    /// </summary>
    /// <param name="instance">The workflow instance</param>
    /// <param name="currentWorkflow">The current workflow definition</param>
    /// <param name="transitionInfo">Optional transition info (used when called from main method)</param>
    /// <returns>Tuple containing available transitions, current state, and status from main flow</returns>
    private (List<string> AvailableTransitions, string? CurrentState, InstanceStatus? Status) GetMainFlowTransitions(
        Instance instance,
        BBT.Workflow.Definitions.Workflow currentWorkflow,
        (InstanceStatus Status, string? CurrentState, List<InstanceCorrelationInfo> ActiveCorrelations)? transitionInfo = null)
    {
        var availableTransitions = new List<string>();

        if (instance.Status.Equals(InstanceStatus.Active))
        {
            availableTransitions = stateMachineService.AvailableUserTransitionKeys(currentWorkflow, instance);
        }

        var currentState = transitionInfo?.CurrentState ?? instance.CurrentState;
        var status = transitionInfo?.Status ?? instance.Status;

        return (availableTransitions, currentState, status);
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
        var correlations = await instanceCorrelationRepository.GetActiveByParentAsync(instanceId, cancellationToken);

        return correlations
            .Select(c => new InstanceCorrelationInfo
            {
                CorrelationId = c.Id,
                ParentState = c.ParentState,
                SubFlowInstanceId = c.SubFlowInstanceId,
                SubFlowType = c.SubFlowType,
                SubFlowDomain = c.SubFlowDomain,
                SubFlowName = c.SubFlowName,
                SubFlowVersion = c.SubFlowVersion,
                IsCompleted = c.IsCompleted
            })
            .ToList();
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

        // Build the data URL
        // var dataUrl = BuildDataUrl(domain, workflow, instance);

        var response = new GetInstanceOutput
        {
            Id = instance.Id,
            Flow = instance.Flow,
            FlowVersion = instanceData?.Version ?? string.Empty,
            Etag = instanceData?.ETag ?? string.Empty,
            Domain = domain,
            // DataUrl = dataUrl,
            Key = instance.Key!,
            Tags = instance.Tags,
            Attributes = instanceData?.Data.JsonElement
        };


        var scriptContext = await scriptContextFactory.NewBuilder()
            .WithWorkflow(flow)
            .WithInstance(instance)
            .WithRuntime(runtimeInfoProvider)
            .WithTransition(string.Empty)
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

    private string BuildDataUrl(string domain, string workflow, Instance instance)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return $"/api/v1/{domain}/workflows/{workflow}/instances/{instance.Id}/data";
        }

        var request = httpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        return $"{baseUrl}/api/v1/{domain}/workflows/{workflow}/instances/{instance.Id}/data";
    }

    public async Task<InstanceServiceResult<GetInstanceDataOutput>> GetInstanceDataAsync(
        GetInstanceDataInput input,
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
                return InstanceServiceResult<GetInstanceDataOutput>.NotModified();
            }

            var result = new GetInstanceDataOutput
            {
                Attributes = instance.LatestData?.Data.JsonElement,
                Etag = instance.LatestData?.ETag ?? string.Empty
            };

            return InstanceServiceResult<GetInstanceDataOutput>.Success(result);
        }
    }


    public async Task<InstanceServiceResponse<GetAvailableSysGetViewOutput>> GetAvailableSysGetViewAsync(
        GetAvailableSysGetViewInput input,
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

            // Get workflow for available transitions
            var currentWorkflow = await componentCacheStore.GetFlowAsync(
                input.Domain,
                input.Workflow,
                input.Version,
                cancellationToken);

            // Build instance transition information using shared logic
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

            // Check if there are any active SubFlow correlations
            var activeSubFlowCorrelation = transitionInfo.ActiveCorrelations
                .Where(c => c.SubFlowType == SubFlowType.SubFlow && !c.IsCompleted)
                .OrderByDescending(c => c.CorrelationId) // Get the latest active SubFlow
                .FirstOrDefault();

            (List<string> availableTransitions, string? currentState, InstanceStatus? status) = activeSubFlowCorrelation != null
                ? await GetSubFlowTransitionsAsync(activeSubFlowCorrelation, instance, currentWorkflow, cancellationToken)
                : GetMainFlowTransitions(instance, currentWorkflow, transitionInfo);

            // Build transition items with href links
            var transitionItems = availableTransitions.Select(transitionKey => new TransitionItem
            {
                Name = transitionKey,
                Href = string.Format(InstanceUrlTemplates.Transition, input.Domain, input.Workflow, instance.Id, transitionKey)
            }).ToList();

            // Build data href
            var dataHref = new DataHref
            {
                Href = string.Format(InstanceUrlTemplates.Data, input.Domain, input.Workflow, instance.Id)
            };

            // Build view href
            var viewHref = new ViewHref
            {
                Href = string.Format(InstanceUrlTemplates.View, input.Domain, input.Workflow, instance.Id),
                LoadData = true
            };

            // Build active correlations with href links
            var activeCorrelationHrefs = transitionInfo.ActiveCorrelations.Select(correlation => new ActiveCorrelationHref
            {
                CorrelationId = correlation.CorrelationId,
                ParentState = correlation.ParentState,
                SubFlowInstanceId = correlation.SubFlowInstanceId,
                SubFlowType = correlation.SubFlowType,
                SubFlowDomain = correlation.SubFlowDomain,
                SubFlowName = correlation.SubFlowName,
                SubFlowVersion = correlation.SubFlowVersion,
                IsCompleted = correlation.IsCompleted,
                Href = string.Format(InstanceUrlTemplates.SubFlowData, correlation.SubFlowDomain, correlation.SubFlowName, correlation.SubFlowInstanceId)
            }).ToList();

            var result = new GetAvailableSysGetViewOutput
            {
                Items = transitionItems,
                Status = status,
                CurrentState = currentState,
                Data = dataHref,
                View = viewHref,
                ActiveCorrelations = activeCorrelationHrefs
            };

            return new InstanceServiceResponse<GetAvailableSysGetViewOutput>(result);
        }
    }

    public async Task<InstanceServiceResponse<GetTransitionItemsOutput>> GetTransitionItemsAsync(
        GetTransitionItemsInput input,
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

            // Get workflow for available transitions
            var currentWorkflow = await componentCacheStore.GetFlowAsync(
                input.Domain,
                input.Workflow,
                input.Version,
                cancellationToken);

            // Build instance transition information using shared logic
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

            // Check if there are any active SubFlow correlations
            var activeSubFlowCorrelation = transitionInfo.ActiveCorrelations
                .Where(c => c.SubFlowType == SubFlowType.SubFlow && !c.IsCompleted)
                .OrderByDescending(c => c.CorrelationId) // Get the latest active SubFlow
                .FirstOrDefault();

            (List<string> availableTransitions, string? currentState, InstanceStatus? status) = activeSubFlowCorrelation != null
                ? await GetSubFlowTransitionsAsync(activeSubFlowCorrelation, instance, currentWorkflow, cancellationToken)
                : GetMainFlowTransitions(instance, currentWorkflow, transitionInfo);

            // Build transition items with href links
            var transitionItems = availableTransitions.Select(transitionKey => new TransitionItem
            {
                Name = transitionKey,
                Href = string.Format(InstanceUrlTemplates.Transition, input.Domain, input.Workflow, instance.Id, transitionKey)
            }).ToList();

            var result = new GetTransitionItemsOutput
            {
                Items = transitionItems,
                Status = status,
                CurrentState = currentState
            };

            return new InstanceServiceResponse<GetTransitionItemsOutput>(result);
        }
    }

    public async Task<InstanceServiceResponse<GetActiveCorrelationsOutput>> GetActiveCorrelationsAsync(
        GetActiveCorrelationsInput input,
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

            // Build instance transition information using shared logic
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

            // Build active correlations with href links
            var activeCorrelationHrefs = transitionInfo.ActiveCorrelations.Select(correlation => new ActiveCorrelationHref
            {
                CorrelationId = correlation.CorrelationId,
                ParentState = correlation.ParentState,
                SubFlowInstanceId = correlation.SubFlowInstanceId,
                SubFlowType = correlation.SubFlowType,
                SubFlowDomain = correlation.SubFlowDomain,
                SubFlowName = correlation.SubFlowName,
                SubFlowVersion = correlation.SubFlowVersion,
                IsCompleted = correlation.IsCompleted,
                Href = $"/{correlation.SubFlowDomain}/workflows/{correlation.SubFlowName}/instances/{correlation.SubFlowInstanceId}/data"
            }).ToList();

            var result = new GetActiveCorrelationsOutput
            {
                ActiveCorrelations = activeCorrelationHrefs,
                Status = transitionInfo.Status,
                CurrentState = transitionInfo.CurrentState
            };

            return new InstanceServiceResponse<GetActiveCorrelationsOutput>(result);
        }
    }

    public async Task<InstanceServiceResponse<GetViewOutput>> GetViewAsync(
        GetViewInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            // Get the instance
            var instance = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (instance == null)
            {
                throw new EntityNotFoundException(typeof(Instance), input.Instance);
            }

            // Get workflow for available transitions
            var currentWorkflow = await componentCacheStore.GetFlowAsync(
                input.Domain,
                input.Workflow,
                input.Version,
                cancellationToken);

            // Get available transitions
            var availableTransitions = new List<string>();
            if (instance.Status.Equals(InstanceStatus.Active))
            {
                availableTransitions = stateMachineService.AvailableUserTransitionKeys(currentWorkflow, instance);
            }

            View? view = null;

            // If there's exactly one transition, get its view
            if (availableTransitions.Count == 1 && !string.IsNullOrEmpty(instance.CurrentState))
            {
                var currentState = currentWorkflow.GetState(instance.CurrentState);
                var transition = currentState.FindTransition(availableTransitions[0]);
                if (transition?.view != null)
                {
                    view = await componentCacheStore.GetViewAsync(
                        transition.view.Domain,
                        transition.view.Key,
                        transition.view.Version,
                        cancellationToken);
                }
            }
            // If there are multiple transitions or no transitions, get the state view
            else if (!string.IsNullOrEmpty(instance.CurrentState))
            {
                var currentState = currentWorkflow.GetState(instance.CurrentState);
                if (currentState.View != null)
                {
                    view = await componentCacheStore.GetViewAsync(
                        currentState.View.Domain,
                        currentState.View.Key,
                        currentState.View.Version,
                        cancellationToken);
                }
            }

            var result = new GetViewOutput
            {
                Content = view?.JsonContent?.RootElement,
                Type = view?.Type.ToString(),
                Target = view?.Target.ToString()
            };

            return new InstanceServiceResponse<GetViewOutput>(result);
        }
    }

    public async Task<InstanceServiceResponse<GetInstanceStateOutput>> GetInstanceStateAsync(
        GetInstanceStateInput input,
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

            // Get workflow for available transitions
            var currentWorkflow = await componentCacheStore.GetFlowAsync(
                input.Domain,
                input.Workflow,
                input.Version,
                cancellationToken);

            // Build instance transition information using shared logic
            var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, cancellationToken);

            // Check if there are any active SubFlow correlations
            var activeSubFlowCorrelation = transitionInfo.ActiveCorrelations
                .Where(c => c.SubFlowType == SubFlowType.SubFlow && !c.IsCompleted)
                .OrderByDescending(c => c.CorrelationId) // Get the latest active SubFlow
                .FirstOrDefault();

            (List<string> availableTransitions, string? currentState, InstanceStatus? status) = activeSubFlowCorrelation != null
                ? await GetSubFlowTransitionsAsync(activeSubFlowCorrelation, instance, currentWorkflow, cancellationToken)
                : GetMainFlowTransitions(instance, currentWorkflow, transitionInfo);

            // Build transition items with href links
            var transitionItems = availableTransitions.Select(transitionKey => new TransitionItem
            {
                Name = transitionKey,
                Href = string.Format(InstanceUrlTemplates.Transition, input.Domain, input.Workflow, instance.Id, transitionKey)
            }).ToList();

            // Build data href with extensions
            var dataHref = new DataHref
            {
                Href = input.Extension?.Length > 0
                    ? string.Format(InstanceUrlTemplates.DataWithExtensions, input.Domain, input.Workflow, instance.Id, string.Join(",", input.Extension))
                    : string.Format(InstanceUrlTemplates.Data, input.Domain, input.Workflow, instance.Id)
            };

            // Build view href
            var viewHref = new ViewHref
            {
                Href = string.Format(InstanceUrlTemplates.View, input.Domain, input.Workflow, instance.Id),
                LoadData = true
            };

            // Build active correlations with href links
            var activeCorrelationHrefs = transitionInfo.ActiveCorrelations.Select(correlation => new ActiveCorrelationHref
            {
                CorrelationId = correlation.CorrelationId,
                ParentState = correlation.ParentState,
                SubFlowInstanceId = correlation.SubFlowInstanceId,
                SubFlowType = correlation.SubFlowType,
                SubFlowDomain = correlation.SubFlowDomain,
                SubFlowName = correlation.SubFlowName,
                SubFlowVersion = correlation.SubFlowVersion,
                IsCompleted = correlation.IsCompleted,
                Href = string.Format(InstanceUrlTemplates.SubFlowData, correlation.SubFlowDomain, correlation.SubFlowName, correlation.SubFlowInstanceId)
            }).ToList();

            var result = new GetInstanceStateOutput
            {
                Data = dataHref,
                View = viewHref,
                State = currentState ?? string.Empty,
                Status = status,
                ActiveCorrelations = activeCorrelationHrefs,
                Transitions = transitionItems,
                ETag = instance.LatestData?.ETag ?? string.Empty
            };

            return new InstanceServiceResponse<GetInstanceStateOutput>(result);
        }
    }
    public async Task<InstanceServiceResponse<GetViewOutput>> GetPlatformSpecificViewAsync(
        GetViewInput input,
        string? platform,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            // Get the instance
            var instance = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (instance == null)
            {
                throw new EntityNotFoundException(typeof(Instance), input.Instance);
            }

            // Get workflow for available transitions
            var currentWorkflow = await componentCacheStore.GetFlowAsync(
                input.Domain,
                input.Workflow,
                input.Version,
                cancellationToken);
            // Get current  state
            var currentState = currentWorkflow.GetState(instance.CurrentState!);
            bool IsWizardState = false;
            if (currentState.StateType == StateType.Wizard)
            {
                IsWizardState = true;
            }
            // Get available transitions
            var availableTransitions = new List<string>();

            if (instance.Status.Equals(InstanceStatus.Active))
            {
                availableTransitions = stateMachineService.AvailableUserTransitionKeys(currentWorkflow, instance);
            }

            View? view = null;

            // If there's exactly one transition, get its view
            if (IsWizardState)
            {
                var transition = currentState.FindTransition(availableTransitions[0]);
                if (transition?.view != null)
                {
                    view = await componentCacheStore.GetViewAsync(
                        transition.view.Domain,
                        transition.view.Key,
                        transition.view.Version,
                        cancellationToken);
                }
            }
            // If there are multiple transitions or no transitions, get the state view
            else if (!string.IsNullOrEmpty(instance.CurrentState) && currentState.View != null)
            {

                view = await componentCacheStore.GetViewAsync(
                    currentState.View.Domain,
                    currentState.View.Key,
                    currentState.View.Version,
                    cancellationToken);

            }

            // If no platform specified, return the default view content
            if (string.IsNullOrEmpty(platform) || view?.PlatformOverrides == null)
            {
                var result = new GetViewOutput
                {
                    Content = view?.JsonContent?.RootElement,
                    Type = view?.Type.ToString(),
                    Target = view?.Target.ToString()
                };
                return new InstanceServiceResponse<GetViewOutput>(result);
            }

            // Handle platform-specific content
            var platformLower = platform.ToLowerInvariant();
            JsonElement? platformContent = null;

            switch (platformLower)
            {
                case PlatformConst.android:
                    platformContent = view.PlatformOverrides.Android?.JsonContent?.RootElement;
                    break;
                case PlatformConst.web:
                    platformContent = view.PlatformOverrides.Web?.JsonContent?.RootElement;
                    break;
                case PlatformConst.ios:
                    platformContent = view.PlatformOverrides.Ios?.JsonContent?.RootElement;
                    break;
                default:
                    platformContent = view?.JsonContent?.RootElement;
                    break;
            }

            // If platform-specific content is not available, fall back to default content
            if (!platformContent.HasValue)
            {
                platformContent = view?.JsonContent?.RootElement;
            }

            var platformResult = new GetViewOutput
            {
                Content = platformContent,
                Type = view?.Type.ToString(),
                Target = view?.Target.ToString()
            };

            return new InstanceServiceResponse<GetViewOutput>(platformResult);
        }
    }

}