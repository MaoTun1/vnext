using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BBT.Workflow.Execution.Transitions.Factory;

/// <summary>
/// Factory implementation for creating TransitionExecutionContext instances.
/// Handles rehydration of workflow definitions, instances, and validation.
/// </summary>
public sealed class TransitionContextFactory(
    IInstanceRepository instanceRepository,
    IComponentCacheStore componentCacheStore,
    IRuntimeInfoProvider runtimeInfoProvider,
    ILogger<TransitionContextFactory> logger) : ITransitionContextFactory
{
    /// <inheritdoc />
    public async Task<Result<TransitionExecutionContext>> CreateAsync(
        WorkflowExecutionContext input, 
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating transition context for instance {InstanceId}, transition {TransitionKey}",
            input.InstanceId, input.TransitionKey);
        
        // Step 1: Validate domain first
        var domainCheck = runtimeInfoProvider.Check(input.Domain);
        if (!domainCheck.IsSuccess)
            return Result<TransitionExecutionContext>.Fail(domainCheck.Error);

        // Step 2-5: Load workflow, rehydrate instance, resolve state/transition, build context
        return await LoadWorkflowAsync(input, cancellationToken)
            .ThenAsync(workflow => RehydrateInstanceAsync(workflow, input, cancellationToken))
            .ThenAsync(data => ResolveStateAndTransitionAsync(data, input))
            .MapAsync(data => BuildExecutionContext(data, input))
            .OnSuccess(ctx => 
                logger.LogDebug("Created transition context for {WorkflowKey}.{TransitionKey} on instance {InstanceId}",
                    ctx.WorkflowKey, ctx.TransitionKey, ctx.InstanceId));
    }

    /// <summary>
    /// Loads the workflow definition.
    /// </summary>
    private async Task<Result<Definitions.Workflow>> LoadWorkflowAsync(
        WorkflowExecutionContext input,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct => await componentCacheStore.GetFlowAsync(
                input.Domain, input.WorkflowKey, input.WorkflowVersion, ct),
            cancellationToken,
            ex => WorkflowErrors.WorkflowNotFound(input.WorkflowKey, input.WorkflowVersion));
    }

    /// <summary>
    /// Rehydrates the instance from storage.
    /// </summary>
    private async Task<Result<(Definitions.Workflow Workflow, Instance Instance)>> RehydrateInstanceAsync(
        Definitions.Workflow workflow,
        WorkflowExecutionContext input,
        CancellationToken cancellationToken)
    {
        var instanceResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.GetActiveAsync(input.InstanceId, ct),
            cancellationToken,
            ex => WorkflowErrors.InstanceNotFound(input.InstanceId, "not found or is not active"));

            return instanceResult.Map(instance => (workflow, instance));
    }

    /// <summary>
    /// Resolves the current state and transition.
    /// </summary>
    private Task<Result<(Definitions.Workflow Workflow, Instance Instance, State CurrentState, Transition? Transition)>> 
        ResolveStateAndTransitionAsync(
            (Definitions.Workflow Workflow, Instance Instance) data,
            WorkflowExecutionContext input)
    {
        var currentStateResult = data.Workflow.GetState(data.Instance.GetCurrentState);
        if (!currentStateResult.IsSuccess)
            return Task.FromResult(
                Result<(Definitions.Workflow, Instance, State, Transition?)>.Fail(currentStateResult.Error));

        var currentState = currentStateResult.Value!;
        var transition = ResolveTransition(data.Workflow, currentState, input.TransitionKey);

        return Task.FromResult(
            Result<(Definitions.Workflow, Instance, State, Transition?)>.Ok(
                (data.Workflow, data.Instance, currentState, transition)));
    }

    /// <summary>
    /// Builds the final TransitionExecutionContext.
    /// </summary>
    private TransitionExecutionContext BuildExecutionContext(
        (Definitions.Workflow Workflow, Instance Instance, State CurrentState, Transition? Transition) data,
        WorkflowExecutionContext input)
    {
        var (traceId, spanId) = InitializeTelemetry();

        var executionContext = new TransitionExecutionContext
        {
            // Identity
            Domain = input.Domain,
            InstanceId = data.Instance.Id,
            WorkflowKey = data.Workflow.Key,
            TransitionKey = data.Transition?.Key ?? input.TransitionKey,
            Trigger = input.TriggerType,
            Actor = input.Actor,
            CorrelationId = input.CorrelationId ?? Guid.NewGuid().ToString("N"),
            CausationId = input.CausationId,
            ExecutionChainId = input.Execution?.ExecutionChainId ?? Guid.NewGuid().ToString("N"),
            ChainDepth = input.Execution?.ChainDepth ?? 0,
            RequestedAt = input.RequestedAt ?? DateTimeOffset.UtcNow,

            // Definitions
            Workflow = data.Workflow,
            Current = data.CurrentState,
            Transition = data.Transition,

            // Instance state
            Instance = data.Instance,
            ConcurrencyToken = data.Instance.ConcurrencyStamp,
            Data = input.Data,

            // Flags
            IsReentry = input.IsReentry,

            // Telemetry
            TraceId = traceId,
            SpanId = spanId,
            Headers = new Dictionary<string, string?>(input.Headers)
        };

        // Configure pipeline directives
        if (input.Execution?.ResumeFrom.HasValue == true)
            executionContext.Directives.RequestResumeFrom(input.Execution.ResumeFrom.Value);
        
        if (input.Execution?.IsSubFlowResume == true)
            executionContext.Directives.MarkAsSubFlowResume();

        return executionContext;
    }

    /// <summary>
    /// Resolves and validates the transition for the given trigger type.
    /// </summary>
    private static Transition? ResolveTransition(
        Definitions.Workflow workflow,
        State currentState,
        string transitionKey)
    {
        return workflow.ResolveTransition(transitionKey, currentState) ?? workflow.FindTransitionInContext(transitionKey);
    }

    /// <summary>
    /// Initializes telemetry context for distributed tracing.
    /// </summary>
    private static (string TraceId, string SpanId) InitializeTelemetry()
    {
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var spanId = activity?.SpanId.ToString() ?? Guid.NewGuid().ToString("N")[..16];
        
        return (traceId, spanId);
    }
}
