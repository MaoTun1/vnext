using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
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
    public async Task<TransitionExecutionContext> CreateAsync(
        WorkflowExecutionContext input, 
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating transition context for instance {InstanceId}, transition {TransitionKey}",
            input.InstanceId, input.TransitionKey);

        // 1. Validate domain/tenant and set schema context
        runtimeInfoProvider.Check(input.Domain); // TODO: May be remove ??

        // 2. Resolve workflow definition (use instance version if not specified)
        var workflow = await componentCacheStore.GetFlowAsync(
            input.Domain, input.WorkflowKey, input.WorkflowVersion, cancellationToken);
        
        // 3. Rehydrate instance with state and data
        var instance = await instanceRepository.GetActiveAsync(input.InstanceId, cancellationToken);

        // 4. Resolve current state and transition
        var currentState = workflow.GetState(instance.GetCurrentState);
        var transition = ResolveTransition(workflow, currentState, input.TransitionKey, input.TriggerType);

        // 5. Initialize telemetry context
        var (traceId, spanId) = InitializeTelemetry();

        // 6. Build the context
        var executionContext = new TransitionExecutionContext
        {
            // Identity
            Domain = input.Domain,
            InstanceId = instance.Id,
            WorkflowKey = workflow.Key,
            TransitionKey = transition.Key,
            Trigger = input.TriggerType,
            Actor = input.Actor,
            CorrelationId = input.CorrelationId ?? Guid.NewGuid().ToString("N"),
            CausationId = input.CausationId,
            ExecutionChainId = input.Execution?.ExecutionChainId ?? Guid.NewGuid().ToString("N"),
            ChainDepth = input.Execution?.ChainDepth ?? 0,
            RequestedAt = input.RequestedAt ?? DateTimeOffset.UtcNow,

            // Definitions
            Workflow = workflow,
            Current = currentState,
            Transition = transition,

            // Instance state
            Instance = instance,
            ConcurrencyToken = instance.ConcurrencyStamp,
            Data = input.Data,

            // Flags
            IsReentry = input.IsReentry,

            // Telemetry
            TraceId = traceId,
            SpanId = spanId,
            Headers = new Dictionary<string, string>(input.Headers)
        };

        // 7. Configure pipeline directives based on execution info
        if (input.Execution?.ResumeFrom.HasValue == true)
        {
            executionContext.Directives.RequestResumeFrom(input.Execution.ResumeFrom.Value);
        }
        
        if (input.Execution?.IsSubFlowResume == true)
        {
            executionContext.Directives.MarkAsSubFlowResume();
        }

        logger.LogDebug("Created transition context for {WorkflowKey}.{TransitionKey} on instance {InstanceId}",
            workflow.Key, transition.Key, instance.Id);

        return executionContext;
    }

    /// <summary>
    /// Resolves and validates the transition for the given trigger type.
    /// </summary>
    private static Transition ResolveTransition(
        Definitions.Workflow workflow,
        State currentState,
        string transitionKey,
        TriggerType triggerType)
    {
        var transition = workflow.ResolveTransition(transitionKey, currentState) ?? workflow.FindTransitionInContext(transitionKey);
        if (transition == null)
        {
            throw new InvalidOperationException($"Cannot resolve transition {transitionKey} from workflow {workflow.Key}.");
        }
        
        // Validate that the trigger type is appropriate for this transition
        ValidateTriggerType(transition, triggerType);
        
        return transition;
    }

    /// <summary>
    /// Validates that the trigger type is appropriate for the transition.
    /// </summary>
    private static void ValidateTriggerType(Transition transition, TriggerType triggerType)
    {
        // For now, we allow all trigger types for all transitions
        // This can be extended with specific validation rules if needed
        // For example, some transitions might only be allowed for manual triggers
        
        // Example validation (commented out):
        // if (transition.IsSystemOnly && triggerType == TriggerType.Manual)
        //     throw new InvalidOperationException($"Transition {transition.Key} can only be triggered by system");
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
