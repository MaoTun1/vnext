using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Policies;
using BBT.Workflow.Runtime;
using BBT.Workflow.Shared;
using BBT.Workflow.Validation;
using System.Diagnostics;
using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Validation;

/// <inheritdoc />
public class TransitionValidationService(
    StateTransitionPolicy stateTransitionPolicy,
    IJsonSchemaValidator schemaValidator,
    IComponentCacheStore componentCacheStore) : ITransitionValidationService
{
    /// <inheritdoc />
    /// <summary>
    /// Validates a transition execution context.
    /// Railway chain: Schema Validation → Policy Validation
    /// </summary>
    public async Task<Result> ValidateAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var schemaResult = await ValidateTransitionSchemaAsync(context, cancellationToken);
        if (!schemaResult.IsSuccess)
            return schemaResult;

        return await ValidateTransitionPolicyAsync(context, context.Actor);
    }

    /// <summary>
    /// Validates transition policies and authorization.
    /// Returns early for skip scenarios (SubFlow resume, null transition, active SubFlow).
    /// </summary>
    private Task<Result> ValidateTransitionPolicyAsync(
        TransitionExecutionContext context,
        ExecutionActor executionActor)
    {
        // Skip validation for special scenarios
        if (ShouldSkipPolicyValidation(context))
            return Task.FromResult(Result.Ok());

        var result = context.Instance.CanExecuteTransition(
            context.Transition!,
            context.Current,
            stateTransitionPolicy,
            executionActor);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Determines if policy validation should be skipped.
    /// </summary>
    private static bool ShouldSkipPolicyValidation(TransitionExecutionContext context)
        => context.Directives.IsSubFlowResume
           || context.Transition is null
           || context.Instance.HasActiveSubFlow;

    /// <summary>
    /// Validates transition data against JSON schema.
    /// Chains GetSchemaAsync Result into validation.
    /// </summary>
    private async Task<Result> ValidateTransitionSchemaAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Guard: No schema defined
        if (context.Transition?.Schema is null)
            return Result.Ok();

        var schemaResult = await componentCacheStore.GetSchemaAsync(
            context.Transition.Schema, cancellationToken);

        if (!schemaResult.IsSuccess)
            return Result.Fail(schemaResult.Error);

        return schemaValidator.Validate(schemaResult.Value!.Schema, context.DataElement);
    }

    /// <inheritdoc />
    /// <summary>
    /// Validates a start transition.
    /// Railway chain: Get Initial State → Build Context → Validate
    /// </summary>
    public async Task<Result> ValidateStartTransitionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        Transition transition,
        object? data,
        IRuntimeInfoProvider runtimeInfoProvider,
        IReadOnlyDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var contextResult = workflow.GetInitialState()
            .Map(initialState => BuildStartTransitionContext(
                workflow, instance, transition, initialState,
                data, runtimeInfoProvider, headers));

        if (!contextResult.IsSuccess)
            return Result.Fail(contextResult.Error);

        return await ValidateAsync(contextResult.Value!, cancellationToken);
    }

    /// <summary>
    /// Builds a TransitionExecutionContext for start transition validation.
    /// </summary>
    private TransitionExecutionContext BuildStartTransitionContext(
        Definitions.Workflow workflow,
        Instance instance,
        Transition transition,
        State initialState,
        object? data,
        IRuntimeInfoProvider runtimeInfoProvider,
        IReadOnlyDictionary<string, string?>? headers)
    {
        var (traceId, spanId) = InitializeTelemetry();

        return new TransitionExecutionContext
        {
            // Identity
            Domain = runtimeInfoProvider.Domain,
            InstanceId = instance.Id,
            WorkflowKey = workflow.Key,
            TransitionKey = transition.Key,
            Trigger = TriggerType.Manual, // Start transitions are always manual
            Actor = ExecutionActor.User, // Start transitions are always user-initiated
            CorrelationId = Guid.NewGuid().ToString("N"),
            CausationId = null,
            ExecutionChainId = Guid.NewGuid().ToString("N"),
            ChainDepth = 0,
            RequestedAt = DateTimeOffset.UtcNow,

            // Definitions
            Workflow = workflow,
            Current = initialState,
            Transition = transition,

            // Instance state
            Instance = instance,
            Data = data,

            // Flags
            IsReentry = false, // Start transitions are never re-entry

            // Telemetry
            TraceId = traceId,
            SpanId = spanId,
            Headers = headers ?? new Dictionary<string, string?>()
        };
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