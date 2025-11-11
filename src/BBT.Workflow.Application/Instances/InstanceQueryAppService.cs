using BBT.Aether.Application.Services;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Extentions;
using BBT.Workflow.Instances.Remote;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Scripting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
    IScriptContextFactory scriptContextFactory,
    IHttpContextAccessor httpContextAccessor,
    IRemoteInstanceQueryAppService remoteInstanceQueryAppService,
    ILogger<InstanceQueryAppService> logger)
    : ApplicationService(serviceProvider), IInstanceQueryAppService
{
    public async Task<ConditionalResult<GetInstanceOutput>> GetInstanceAsync(
        GetInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var instanceResult = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (!instanceResult.IsSuccess)
            {
                return ConditionalResult<GetInstanceOutput>.Fail(instanceResult.Error);
            }

            var instance = instanceResult.Value!;

            // Check ETag for conditional requests
            if (!string.IsNullOrEmpty(input.IfNoneMatch) &&
                instance.LatestData != null &&
                instance.LatestData.ETag.MatchesIfNoneMatch(input.IfNoneMatch))
            {
                return ConditionalResult<GetInstanceOutput>.NotModified();
            }

            var result = await BuildInstanceOutputAsync(
                input.Domain,
                input.Extension,
                input.Workflow,
                instance,
                instance.LatestData,
                ExtensionScope.GetInstance,
                cancellationToken);

            return ConditionalResult<GetInstanceOutput>.Success(result);
        }
    }

    public async Task<Result<PaginationResult<GetInstanceOutput>>> GetInstanceListAsync(
        GetInstanceListInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            return await ResultExtensions.TryAsync(
                async ct =>
                {
                    var filteredQuery = await instanceRepository.GetFilteredQueryAsync(
                        input.Filter,
                        ct);

                    // Then apply pagination to the filtered query
                    var pagedResult = await instanceRepository.GetPagedResultsAsync(
                        input.Page,
                        input.PageSize,
                        input.PageUrl,
                        httpContextAccessor.HttpContext?.Request.Query,
                        filteredQuery,
                        ct);

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
                            ct);

                        result.Data.Add(instanceOutput);
                    }

                    return result;
                },
                cancellationToken);
        }
    }

    public async Task<Result<GetInstanceHistoryOutput>> GetInstanceHistoryAsync(
        GetInstanceHistoryInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var instanceResult = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (!instanceResult.IsSuccess)
            {
                return Result<GetInstanceHistoryOutput>.Fail(instanceResult.Error);
            }

            var instance = instanceResult.Value!;

            return await ResultExtensions.TryAsync(
                async ct =>
                {
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
                            ct);

                        transitions.Add(transitionOutput);
                    }

                    return new GetInstanceHistoryOutput
                    {
                        Transitions = transitions
                    };
                },
                cancellationToken);
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
    private async Task<(List<string> AvailableTransitions, string? CurrentState, InstanceStatus? Status)>
        GetSubFlowTransitionsAsync(
            InstanceCorrelationInfo activeSubFlowCorrelation,
            Instance mainInstance,
            BBT.Workflow.Definitions.Workflow currentWorkflow,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var subFlowInput = new GetFunctionWithInstanceInput
            {
                Domain = activeSubFlowCorrelation.SubFlowDomain,
                Workflow = activeSubFlowCorrelation.SubFlowName,
                Version = activeSubFlowCorrelation.SubFlowVersion,
                Instance = activeSubFlowCorrelation.SubFlowInstanceId.ToString(),
                Function = Definitions.Functions.FunctionTypeConst.Longpooling // "state"
            };

            var subFlowResult = await remoteInstanceQueryAppService.GetFunctionWithStateAsync(
                subFlowInput,
                cancellationToken);

            if (subFlowResult.IsSuccess && subFlowResult.Value != null)
            {
                // Use SubFlow transitions and current state, but always use main instance status
                // Extract transition names from TransitionItem list
                var transitionNames = subFlowResult.Value.Transitions
                    .Select(t => t.Name)
                    .ToList();
                return (transitionNames, subFlowResult.Value.State, mainInstance.Status);
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
        (InstanceStatus Status, string? CurrentState, List<InstanceCorrelationInfo> ActiveCorrelations)?
            transitionInfo = null)
    {
        var availableTransitions = new List<string>();

        if (instance.Status.Equals(InstanceStatus.Active))
        {
            var stateResult = currentWorkflow.GetState(instance.GetCurrentState);
            if (stateResult.IsSuccess)
            {
                availableTransitions = currentWorkflow.GetAvailableUserTransitionKeys(stateResult.Value!);
            }
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

    private async Task<Result<Instance>> GetInstanceByIdOrKeyAsync(string instanceIdentifier,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                var instance = Guid.TryParse(instanceIdentifier, out var instanceId)
                    ? await instanceRepository.FindByIdAsReadOnlyAsync(instanceId, ct)
                    : await instanceRepository.FindByKeyAsReadOnlyAsync(instanceIdentifier, ct);

                if (instance == null)
                {
                    throw new EntityNotFoundException(typeof(Instance), instanceIdentifier);
                }

                return instance;
            },
            cancellationToken,
            _ => WorkflowErrors.InstanceNotFound(instanceIdentifier));
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

    public async Task<ConditionalResult<GetInstanceDataOutput>> GetInstanceDataAsync(
        GetInstanceDataInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);

        var flowResult = await ResultExtensions.TryAsync(
            async ct => await componentCacheStore.GetFlowAsync(input.Domain, input.Workflow, null, ct),
            cancellationToken,
            _ => WorkflowErrors.WorkflowNotFound(input.Workflow));

        if (!flowResult.IsSuccess)
        {
            return ConditionalResult<GetInstanceDataOutput>.Fail(flowResult.Error);
        }

        var flow = flowResult.Value!;

        using (currentSchema.Change(input.Workflow))
        {
            var instanceResult = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (!instanceResult.IsSuccess)
            {
                return ConditionalResult<GetInstanceDataOutput>.Fail(instanceResult.Error);
            }

            var instance = instanceResult.Value!;

            // Check ETag for conditional requests
            if (!string.IsNullOrEmpty(input.IfNoneMatch) &&
                instance.LatestData != null &&
                instance.LatestData.ETag.MatchesIfNoneMatch(input.IfNoneMatch))
            {
                return ConditionalResult<GetInstanceDataOutput>.NotModified();
            }

            var dataResult = await ResultExtensions.TryAsync(
                async ct =>
                {
                    var result = new GetInstanceDataOutput
                    {
                        Data = instance.LatestData?.Data.JsonElement,
                        Etag = instance.LatestData?.ETag ?? string.Empty
                    };

                    var scriptContext = await scriptContextFactory.NewBuilder()
                        .WithWorkflow(flow)
                        .WithInstance(instance)
                        .WithRuntime(runtimeInfoProvider)
                        .WithTransition(string.Empty)
                        .WithBody(instance.LatestData?.Data ?? new JsonData("{}"))
                        .BuildAsync(ct);

                    result.Extensions = await instanceExtensionService.ProcessExtensionsAsync(
                        input.Extensions,
                        scriptContext,
                        flow,
                        ExtensionScope.GetInstance,
                        ct);

                    return result;
                },
                cancellationToken);

            if (!dataResult.IsSuccess)
            {
                return ConditionalResult<GetInstanceDataOutput>.Fail(dataResult.Error);
            }

            return ConditionalResult<GetInstanceDataOutput>.Success(dataResult.Value!);
        }
    }

    /// <summary>
    /// Gets the view definition for platform-specific view retrieval.
    /// Throws <see cref="EntityNotFoundException"/> if view definition is not found.
    /// </summary>
    private ViewDefinition? GetViewDefinition(
        Instance instance,
        Definitions.Workflow currentWorkflow,
        State currentState,
        string? transitionKey = null)
    {
        if (!transitionKey.IsNullOrWhiteSpace())
        {
            var transition = currentWorkflow.ResolveTransition(transitionKey, currentState);
            return transition?.View;
        }

        bool isWizardState = currentState is { StateType: StateType.Wizard };

        // Get available transitions
        var availableTransitions = new List<string>();

        if (instance.Status.Equals(InstanceStatus.Active))
        {
            availableTransitions = currentWorkflow.GetAvailableUserTransitionKeys(currentState);
        }

        ViewDefinition? viewDefinition = null;

        // If there's exactly one transition, get its view
        if (isWizardState)
        {
            var transition = currentState?.FindTransition(availableTransitions[0]);
            viewDefinition = transition?.View;
        }
        // If there are multiple transitions or no transitions, get the state view
        else if (!string.IsNullOrEmpty(instance.CurrentState) && currentState?.View != null)
        {
            viewDefinition = currentState.View;
        }

        return viewDefinition;
    }

    public async Task<Result<GetInstanceStateOutput>> GetInstanceStateAsync(
        GetInstanceStateInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var instanceResult = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (!instanceResult.IsSuccess)
            {
                return Result<GetInstanceStateOutput>.Fail(instanceResult.Error);
            }

            var instance = instanceResult.Value!;

            return await ResultExtensions.TryAsync(
                async ct =>
                {
                    // Get workflow for available transitions
                    var currentWorkflow = await componentCacheStore.GetFlowAsync(
                        input.Domain,
                        input.Workflow,
                        input.Version,
                        ct);

                    // Build instance transition information using shared logic
                    var transitionInfo = await BuildInstanceTransitionInfoAsync(instance, ct);

                    // Check if there are any active SubFlow correlations
                    var activeSubFlowCorrelation = transitionInfo.ActiveCorrelations
                        .Where(c => c.SubFlowType.Equals(SubFlowType.SubFlow) && !c.IsCompleted)
                        .OrderByDescending(c => c.CorrelationId) // Get the latest active SubFlow
                        .FirstOrDefault();

                    (List<string> availableTransitions, string? currentState, InstanceStatus? status) =
                        activeSubFlowCorrelation != null
                            ? await GetSubFlowTransitionsAsync(activeSubFlowCorrelation, instance, currentWorkflow,
                                ct)
                            : GetMainFlowTransitions(instance, currentWorkflow, transitionInfo);

                    // Build transition items with href links
                    var transitionItems = availableTransitions.Select(transitionKey => new TransitionItem
                    {
                        Name = transitionKey,
                        Href = string.Format(InstanceUrlTemplates.Transition, input.Domain, input.Workflow,
                            instance.Id,
                            transitionKey)
                    }).ToList();

                    var currentStateResult = currentWorkflow.GetState(instance.CurrentState!);
                    if (!currentStateResult.IsSuccess || currentStateResult.Value == null)
                    {
                        throw new EntityNotFoundException(
                            typeof(State),
                            $"State {instance.CurrentState} not found in workflow {input.Workflow}");
                    }

                    // Get view definition
                    var viewDefinition = GetViewDefinition(
                        instance,
                        currentWorkflow,
                        currentStateResult.Value);

                    // Build data href with extensions
                    var allExtensions = (input.Extensions ?? []).Concat(viewDefinition?.Extensions ?? []).ToArray();
                    var dataHref = new DataHref
                    {
                        Href = allExtensions.Length > 0
                            ? string.Format(InstanceUrlTemplates.DataWithExtensions, input.Domain, input.Workflow,
                                instance.Id,
                                string.Join(",", allExtensions))
                            : string.Format(InstanceUrlTemplates.Data, input.Domain, input.Workflow, instance.Id)
                    };

                    // Build view href
                    var viewHref = new ViewHref
                    {
                        Href = string.Format(InstanceUrlTemplates.View, input.Domain, input.Workflow, instance.Id),
                        LoadData = viewDefinition?.LoadData == true
                    };

                    // Build active correlations with href links
                    var activeCorrelationHrefs = transitionInfo.ActiveCorrelations.Select(correlation =>
                        new ActiveCorrelationHref
                        {
                            CorrelationId = correlation.CorrelationId,
                            ParentState = correlation.ParentState,
                            SubFlowInstanceId = correlation.SubFlowInstanceId,
                            SubFlowType = correlation.SubFlowType,
                            SubFlowDomain = correlation.SubFlowDomain,
                            SubFlowName = correlation.SubFlowName,
                            SubFlowVersion = correlation.SubFlowVersion,
                            IsCompleted = correlation.IsCompleted,
                            Href = string.Format(InstanceUrlTemplates.Data, correlation.SubFlowDomain,
                                correlation.SubFlowName, correlation.SubFlowInstanceId)
                        }).ToList();

                    return new GetInstanceStateOutput
                    {
                        Data = dataHref,
                        View = viewHref,
                        State = currentState ?? string.Empty,
                        Status = status,
                        ActiveCorrelations = activeCorrelationHrefs,
                        Transitions = transitionItems,
                        ETag = instance.LatestData?.ETag ?? string.Empty
                    };
                },
                cancellationToken,
                ex => ex is EntityNotFoundException
                    ? Error.NotFound("notfound", ex.Message)
                    : Error.Dependency("unexpected", ex.Message));
        }
    }

    public async Task<Result<GetViewOutput>> GetPlatformSpecificViewAsync(
        GetViewInput input,
        string? platform,
        string? transitionKey,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        using (currentSchema.Change(input.Workflow))
        {
            var instanceResult = await GetInstanceByIdOrKeyAsync(input.Instance, cancellationToken);
            if (!instanceResult.IsSuccess)
            {
                return Result<GetViewOutput>.Fail(instanceResult.Error);
            }

            var instance = instanceResult.Value!;

            return await ResultExtensions.TryAsync(
                async ct =>
                {
                    // Get workflow for available transitions
                    var currentWorkflow = await componentCacheStore.GetFlowAsync(
                        input.Domain,
                        input.Workflow,
                        input.Version,
                        ct);

                    // Get current state
                    var currentStateResult = currentWorkflow.GetState(instance.CurrentState!);
                    if (!currentStateResult.IsSuccess || currentStateResult.Value == null)
                    {
                        throw new EntityNotFoundException(
                            typeof(State),
                            $"State {instance.CurrentState} not found in workflow {input.Workflow}");
                    }

                    var currentState = currentStateResult.Value;

                    // If instance has active subflow, handle subflow view logic
                    if (instance.HasActiveSubFlow)
                    {
                        var subFlowViewResult = await GetSubFlowViewWithOverrideAsync(
                            instance,
                            currentState,
                            platform,
                            transitionKey,
                            ct);

                        if (subFlowViewResult != null)
                        {
                            return subFlowViewResult;
                        }
                    }

                    // Get view definition
                    var viewDefinition = GetViewDefinition(
                        instance,
                        currentWorkflow,
                        currentState,
                        transitionKey);

                    if (viewDefinition == null && viewDefinition?.View == null)
                    {
                        throw new EntityNotFoundException(
                            typeof(View),
                            $"View not found for state {instance.CurrentState} in workflow {currentWorkflow.Key}");
                    }

                    var view = await componentCacheStore.GetViewAsync(
                        viewDefinition.View.Domain,
                        viewDefinition.View.Key,
                        viewDefinition.View.Version,
                        ct);

                    // Return platform-specific view for main flow
                    return BuildViewOutput(view, platform);
                },
                cancellationToken,
                ex => ex is EntityNotFoundException
                    ? Error.NotFound("notfound", ex.Message)
                    : Error.Dependency("unexpected", ex.Message));
        }
    }

    /// <summary>
    /// Gets the subflow view with override handling if applicable.
    /// Returns the subflow view if no override is needed, or the overridden view if override exists.
    /// </summary>
    /// <param name="instance">The workflow instance</param>
    /// <param name="currentState">The current state of the workflow</param>
    /// <param name="platform">Platform identifier (optional)</param>
    /// <param name="transitionKey"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>GetViewOutput if subflow view is handled, null if should fall back to main flow view</returns>
    private async Task<GetViewOutput?> GetSubFlowViewWithOverrideAsync(
        Instance instance,
        State currentState,
        string? platform,
        string? transitionKey = null,
        CancellationToken cancellationToken = default)
    {
        var subFlowViewResult = await remoteInstanceQueryAppService.GetFunctionWithViewAsync(
            new GetFunctionWithInstanceInput
            {
                Instance = instance.Subflow!.SubFlowInstanceId.ToString(),
                Domain = instance.Subflow!.SubFlowDomain,
                Workflow = instance.Subflow!.SubFlowName,
                Version = instance.Subflow!.SubFlowVersion,
                Function = "view"
            },
            platform?.ToLowerInvariant(),
            transitionKey,
            cancellationToken);

        if (!subFlowViewResult.IsSuccess)
        {
            return null;
        }

        // If current state has view overrides, use the override view
        if (currentState.SubFlow!.HasViewOverrides)
        {
            var overrideViewRef = currentState.SubFlow!.ViewOverrides!.GetOrDefault(subFlowViewResult.Value!.Key);

            if (overrideViewRef != null)
            {
                var overrideView = await componentCacheStore.GetViewAsync(
                    overrideViewRef.Domain,
                    overrideViewRef.Key,
                    overrideViewRef.Version,
                    cancellationToken);

                return BuildViewOutput(overrideView, platform);
            }
        }

        // Return subflow view directly (remote call already handled platform-specific logic)
        return subFlowViewResult.Value!;
    }

    /// <summary>
    /// Builds a GetViewOutput from a View, applying platform-specific content if available.
    /// </summary>
    /// <param name="view">The view to build output from</param>
    /// <param name="platform">Platform identifier (optional)</param>
    /// <returns>GetViewOutput with platform-specific content if available, otherwise default content</returns>
    private GetViewOutput BuildViewOutput(Definitions.View view, string? platform)
    {
        var content = GetPlatformSpecificContent(view, platform);

        return new GetViewOutput
        {
            Key = view.Key,
            Content = content,
            Type = view.Type.ToString()
        };
    }

    /// <summary>
    /// Extracts platform-specific content from a view based on the platform identifier.
    /// Returns default content if platform is not specified or no platform override exists.
    /// </summary>
    /// <param name="view">The view to extract content from</param>
    /// <param name="platform">Platform identifier (optional)</param>
    /// <returns>Platform-specific content if available, otherwise default view content</returns>
    private string? GetPlatformSpecificContent(Definitions.View view, string? platform)
    {
        // If no platform specified or no platform overrides, return default content
        if (string.IsNullOrEmpty(platform) || view.PlatformOverrides == null)
        {
            return view.Content;
        }

        var platformLower = platform.ToLowerInvariant();

        return platformLower switch
        {
            PlatformConst.Android => view.PlatformOverrides.Android?.Content ?? view.Content,
            PlatformConst.Web => view.PlatformOverrides.Web?.Content ?? view.Content,
            PlatformConst.Ios => view.PlatformOverrides.Ios?.Content ?? view.Content,
            _ => view.Content
        };
    }
}