using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using System.Diagnostics;
using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Transitions.Factory;

/// <inheritdoc />
public sealed class TransitionContextFactory(
    IInstanceRepository instanceRepository,
    IComponentCacheStore componentCacheStore,
    IRuntimeInfoProvider runtimeInfoProvider) : ITransitionContextFactory
{
    /// <inheritdoc />
    public async Task<Result<TransitionExecutionContext>> CreateAsync(
        WorkflowExecutionContext input,
        CancellationToken cancellationToken)
    {
        // Step 1: Validate domain first
        runtimeInfoProvider.Check(input.Domain);

        // Load workflow, rehydrate instance, resolve state/transition, build context
        return await RehydrateInstanceAsync(input, cancellationToken)
            .ThenAsync(data => ResolveStateAndTransitionAsync(data, input))
            .MapAsync(data => BuildExecutionContext(data, input));
    }

    /// <summary>
    /// Rehydrates the instance from storage.
    /// </summary>
    private async Task<Result<(Definitions.Workflow Workflow, Instance Instance)>> RehydrateInstanceAsync(
        WorkflowExecutionContext input,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(async ct =>
        {
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.WorkflowKey, input.WorkflowVersion, ct);
            var instance = await instanceRepository.GetActiveAsync(input.InstanceId, ct);
            return (workflow, instance);
        }, cancellationToken);
    }

    /// <summary>
    /// Resolves the current state and transition.
    /// </summary>
    private Task<Result<(Definitions.Workflow Workflow, Instance Instance, State CurrentState, Transition? Transition)>>
        ResolveStateAndTransitionAsync(
            (Definitions.Workflow Workflow, Instance Instance) data,
            WorkflowExecutionContext input)
    {
        var result = ResultExtensions.Try(() =>
        {
            var currentStateResult = data.Workflow.GetState(data.Instance.GetCurrentState);
            if (!currentStateResult.IsSuccess)
                throw new InvalidOperationException(currentStateResult.Error.Message);
                    
            var currentState = currentStateResult.Value!;

            // In Resume mode, transition is optional (e.g., SubFlow completion scenarios)
            var transition = input.Mode == ExecMode.Resume
                ? null
                : ResolveTransition(data.Workflow, currentState, input.TransitionKey);
            
            return (data.Workflow, data.Instance, currentState, transition);
        });
        
        return Task.FromResult(result);
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
            Data = input.Data,

            // Flags
            IsReentry = input.IsReentry,

            // Telemetry
            TraceId = traceId,
            SpanId = spanId,
            Headers = input.Headers
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
        return workflow.ResolveTransition(transitionKey, currentState) ??
               workflow.FindTransitionInContext(transitionKey);
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