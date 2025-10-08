using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that finalizes the transition execution.
/// Updates the transition record and performs cleanup operations.
/// </summary>
public sealed class FinalizeTransitionStep(
    IInstanceTransitionRepository instanceTransitionRepository,
    IWorkflowMetrics workflowMetrics,
    ILogger<FinalizeTransitionStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Finalize;

    /// <inheritdoc />
    public async Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogDebug("Finalizing transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        // Get the transition record from context
        if (context.Items.TryGetValue("TransitionRecord", out var record) && 
            record is InstanceTransition instanceTransition)
        {
            // Mark the transition as completed
            instanceTransition.Completed(context.Instance.GetCurrentState);

            // Record state duration metric if available
            if (instanceTransition.Duration.HasValue)
            {
                workflowMetrics.RecordStateDuration(
                    context.Workflow.Key,
                    instanceTransition.FromState,
                    instanceTransition.Duration.Value.TotalSeconds);
            }

            // Update the transition record
            await instanceTransitionRepository.UpdateCompletedAsync(instanceTransition, cancellationToken);

            logger.LogDebug("Updated transition record {TransitionId} as completed", instanceTransition.Id);
        }
        else
        {
            logger.LogWarning("No transition record found in context for instance {InstanceId}", context.InstanceId);
        }

        // Perform any additional cleanup
        await PerformCleanupAsync(context, cancellationToken);

        logger.LogDebug("Finalized transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);
    }

    /// <summary>
    /// Performs any additional cleanup operations.
    /// </summary>
    private async Task PerformCleanupAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Dispose ScriptContext if it exists
        if (context.Items.TryGetValue("ScriptContext", out var scriptContextObj) && 
            scriptContextObj is ScriptContext scriptContext)
        {
            scriptContext.Dispose();
        }
        
        // Clear temporary items from context
        context.Items.Clear();

        // Any other cleanup operations can be added here
        await Task.CompletedTask;
    }
}
