using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.SubFlow;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that handles SubFlow operations or workflow completion.
/// Manages workflow finishing and sub-process initiation.
/// </summary>
public sealed class HandleSubFlowOrFinishStep(
    IInstanceRepository instanceRepository,
    ISubFlowService subFlowService,
    ILogger<HandleSubFlowOrFinishStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.FinishOrSubflow;

    /// <inheritdoc />
    public async Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target == null)
        {
            logger.LogWarning("Target state is null for instance {InstanceId}", context.InstanceId);
            return;
        }

        logger.LogDebug("Handling SubFlow or Finish for state {StateName} on instance {InstanceId}",
            context.Target.Key, context.InstanceId);

        // Update instance state and data changes
        await instanceRepository.UpdateAsync(context.Instance, saveChanges: true, cancellationToken);

        // Handle different state types
        if (context.Target.StateType == StateType.Finish)
        {
            await HandleFinishStateAsync(context, cancellationToken);
        }
        else
        {
            await HandleSubFlowAsync(context, cancellationToken);
        }

        logger.LogDebug("Completed SubFlow or Finish handling for state {StateName}", context.Target.Key);
    }

    /// <summary>
    /// Handles workflow finishing logic.
    /// </summary>
    private async Task HandleFinishStateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogDebug("Handling finish state for instance {InstanceId}", context.InstanceId);

        // Mark that we're in a finish state - automatic and scheduled transitions will still be processed
        // but instance status handling will be done later in the pipeline
        context.Items["IsFinishState"] = true;

        await Task.CompletedTask; // Placeholder for any finish-specific logic
    }

    /// <summary>
    /// Handles SubFlow operations.
    /// </summary>
    private async Task HandleSubFlowAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target?.SubFlow == null)
        {
            logger.LogTrace("No SubFlow defined for state {StateName}", context.Target?.Key);
            return;
        }

        logger.LogDebug("Handling SubFlow {SubFlowType} for state {StateName} on instance {InstanceId}",
            context.Target.SubFlow.Type, context.Target.Key, context.InstanceId);

        // Create script context for SubFlow handling
        var scriptContext = context.GetOrBuildScriptContext(() => 
            CreateScriptContext(context));

        // Handle the SubFlow
        await HandleSubFlowAsync(context.Workflow, context.Instance, context.Target, scriptContext, cancellationToken);

        // Store SubFlow information for later pipeline steps
        context.Items["HasSubFlow"] = true;
        context.Items["SubFlowType"] = context.Target.SubFlow.Type;
    }

    /// <summary>
    /// Handles SubFlow processing using the existing SubFlow service.
    /// </summary>
    private async Task HandleSubFlowAsync(
        Definitions.Workflow workflow,
        Instance instance,
        State targetState,
        Scripting.ScriptContext scriptContext,
        CancellationToken cancellationToken)
    {
        // This method would contain the SubFlow handling logic from the original StateMachineExecutor
        // For now, we'll use a placeholder that delegates to the existing service
        
        // TODO: Extract and refactor the SubFlow handling logic from StateMachineExecutor
        // This is a complex operation that involves:
        // 1. SubProcess creation and management
        // 2. SubFlow state synchronization
        // 3. Parent-child workflow relationships
        
        await Task.CompletedTask; // Placeholder
    }

    /// <summary>
    /// Creates a script context for SubFlow operations.
    /// </summary>
    private Scripting.ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        // This would use the script context factory to create a proper context
        // For now, we'll get it from the context cache
        return context.GetOrBuildScriptContext(() => 
            throw new InvalidOperationException("ScriptContext should have been created in previous steps"));
    }
}
