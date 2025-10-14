using System.Diagnostics;
using BBT.Aether.Guids;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.SubFlow;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that handles SubFlow operations.
/// Manages sub-process initiation when the target state type is SubFlow.
/// </summary>
public sealed class HandleSubFlowStep(
    IInstanceRepository instanceRepository,
    ISubflowStarter subflowStarter,
    IGuidGenerator guidGenerator,
    IScriptContextFactory scriptContextFactory,
    ILogger<HandleSubFlowStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.SubFlow;

    /// <inheritdoc />
    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target == null)
        {
            logger.LogWarning("Target state is null for instance {InstanceId}", context.InstanceId);
            return StepOutcome.Continue();
        }

        // Only handle SubFlow state types
        if (context.Target.StateType != StateType.SubFlow)
        {
            logger.LogTrace("State {StateName} is not a SubFlow type, skipping SubFlow handling",
                context.Target.Key);
            return StepOutcome.Continue();
        }

        if (context.Target?.SubFlow == null)
        {
            logger.LogWarning("No SubFlow defined for state {StateName}", context.Target?.Key);
            throw new ConfigInvalidException(context.InstanceId);
        }

        logger.LogDebug("Handling SubFlow for state {StateName} on instance {InstanceId}",
            context.Target.Key, context.InstanceId);

        await HandleSubFlowAsync(context, cancellationToken);

        logger.LogDebug("Completed SubFlow handling for state {StateName}", context.Target.Key);

        // Skip the epilogue, go to the finale, then stop the pipeline
        return new StepOutcome
        {
            MutateDirectives = d =>
            {
                d.RequestEpilogue(EpilogueMode.Skip);
                d.MarkTerminal();
            },
            SkipToOrder = LifecycleOrder.Finalize
        };
    }

    /// <summary>
    /// Handles SubFlow operations.
    /// </summary>
    private async Task HandleSubFlowAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogDebug("Handling SubFlow {SubFlowType} for state {StateName} on instance {InstanceId}",
            context.Target!.SubFlow!.Type, context.Target.Key, context.InstanceId);

        // Handle the SubFlow
        // Create correlation to track SubFlow/SubProcess instance
        // SubFlow (Type "S"): Blocks parent workflow until completion
        // SubProcess (Type "P"): Runs in parallel without blocking parent
        
        //TODO: Bu creation'ı Instance içine al
        var correlation = InstanceCorrelation.Create(
            guidGenerator.Create(),
            context.InstanceId,
            context.Target.Key,
            guidGenerator.Create(),
            context.Target!.SubFlow!.Type.Code,
            context.Target.SubFlow.Process.Domain,
            context.Target.SubFlow.Process.Key,
            context.Target.SubFlow.Process.Version);

        context.Instance.AddCorrelation(correlation);
        await instanceRepository.UpdateAsync(context.Instance, true, cancellationToken);

        // Create script context for SubFlow handling
        var scriptContext = context.GetOrBuildScriptContext(() =>
            CreateScriptContext(context));

        await subflowStarter.StartAsync(
            context.Workflow,
            context.Instance,
            context.Target,
            context.Transition,
            correlation,
            scriptContext,
            cancellationToken
        );

        // Record SubFlow initiation as an event
        Activity.Current?.AddEvent(new ActivityEvent("subflow.initiated",
            tags: new ActivityTagsCollection
            {
                { TelemetryConstants.TagNames.SubFlowKey, context.Target.SubFlow.Process.Key },
                { TelemetryConstants.TagNames.Domain, context.Target.SubFlow.Process.Domain },
                { "workflow.subflow.type", context.Target.SubFlow.Type.Code },
                { "workflow.subflow.version", context.Target.SubFlow.Process.Version?.ToString() ?? "latest" },
                { "workflow.correlation.id", correlation.Id.ToString() },
                { "workflow.subflow.instance.id", correlation.SubFlowInstanceId.ToString() }
            }));

        logger.LogDebug(
            "SubFlow {SubFlowKey} initiated with correlation {CorrelationId} and instance {SubFlowInstanceId}",
            context.Target.SubFlow.Process.Key, correlation.Id, correlation.SubFlowInstanceId);
    }

    /// <summary>
    /// Creates a script context for SubFlow operations.
    /// </summary>
    private ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        // This would use the script context factory to create a proper context
        // For now, we'll get it from the context cache
        return scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithTransition(context.Transition)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value))
            .BuildAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}