using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Execution;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Policies;
using BBT.Workflow.Runtime;
using BBT.Workflow.Shared;
using BBT.Workflow.Validation;
using Microsoft.Extensions.Logging;
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
    public async Task<Result> ValidateAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate data against schema if present
        var schemaResult = await ValidateTransitionSchemaAsync(context, cancellationToken);
        if (!schemaResult.IsSuccess)
        {
            return schemaResult;
        }

        // 2. Validate that the instance can execute this transition (includes authorization check)
        var policyResult = await ValidateTransitionPolicyAsync(context, context.Actor, cancellationToken);
        if (!policyResult.IsSuccess)
        {
            return policyResult;
        }

        return Result.Ok();
    }

    /// <summary>
    /// Validates transition policies and authorization using Result Pattern.
    /// </summary>
    private Task<Result> ValidateTransitionPolicyAsync(
        TransitionExecutionContext context,
        ExecutionActor executionActor,
        CancellationToken cancellationToken)
    {
        // Skip validation for SubFlow resume scenarios or when transition is null
        if (context.Directives.IsSubFlowResume || context.Transition == null)
        {
            return Task.FromResult(Result.Ok());
        }

        if (context.Instance.HasActiveSubFlow)
        {
            return Task.FromResult(Result.Ok());
        }

        var result = context.Instance.CanExecuteTransition(
            context.Transition,
            context.Current,
            stateTransitionPolicy,
            executionActor);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Validates transition data against JSON schema using Result Pattern.
    /// </summary>
    private async Task<Result> ValidateTransitionSchemaAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Transition?.Schema == null)
        {
            return Result.Ok();
        }

        return await ResultExtensions
            .TryAsync(async ct => await componentCacheStore.GetSchemaAsync(context.Transition.Schema, ct),
                cancellationToken)
            .ThenAsync(schema => Task.FromResult(schemaValidator.Validate(schema.Schema, context.DataElement)));
    }

    /// <inheritdoc />
    public async Task<Result> ValidateStartTransitionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        Transition transition,
        object? data,
        IRuntimeInfoProvider runtimeInfoProvider,
        IReadOnlyDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        // Get initial state for the start transition
        var initialStateResult = workflow.GetInitialState();
        if (!initialStateResult.IsSuccess)
        {
            return Result.Fail(initialStateResult.Error);
        }

        var initialState = initialStateResult.Value!;

        // Manually construct TransitionExecutionContext for start transition validation
        var context = BuildStartTransitionContext(
            workflow,
            instance,
            transition,
            initialState,
            data,
            runtimeInfoProvider,
            headers);

        // Reuse existing validation logic
        return await ValidateAsync(context, cancellationToken);
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