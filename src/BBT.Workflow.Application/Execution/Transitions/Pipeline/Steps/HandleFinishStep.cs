using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.SubFlow;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that handles workflow finishing.
/// Manages workflow completion when the target state type is Finish.
/// This step runs after all other pipeline steps to ensure proper completion.
/// </summary>
public sealed class HandleFinishStep(
    IInstanceRepository instanceRepository,
    DaprClient daprClient,
    IConfiguration configuration,
    IWorkflowMetrics workflowMetrics,
    ILogger<HandleFinishStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Finish;

    /// <inheritdoc />
    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target == null)
        {
            logger.LogWarning("Target state is null for instance {InstanceId}", context.InstanceId);
            return StepOutcome.Continue();
        }

        // Only handle Finish state types
        if (context.Target.StateType != StateType.Finish)
        {
            logger.LogTrace("State {StateName} is not a Finish type, skipping finish handling", 
                context.Target.Key);
            return StepOutcome.Continue();
        }

        logger.LogDebug("Handling finish state {StateName} for instance {InstanceId}",
            context.Target.Key, context.InstanceId);

        context.Instance.Complete();
        // Update instance state and data changes
        await instanceRepository.UpdateStatusAsync(context.Instance, cancellationToken);

        await HandleFinishStateAsync(context, cancellationToken);

        logger.LogDebug("Completed finish handling for state {StateName}", context.Target.Key);
        
        return StepOutcome.Continue();
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

        if (context.Instance.ShouldPublishCompletionEvent())
        {
            await PublishFlowCompletionEventAsync(context.Instance, context.Workflow, cancellationToken);
        }
    }
    
    private async Task PublishFlowCompletionEventAsync(
        Instance instance,
        Definitions.Workflow workflow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Publishing flow completion event for instance {InstanceId} in domain {Domain}, workflow {WorkflowKey}",
                instance.Id, workflow.Domain, workflow.Key);

            // Get the latest instance data
            var latestData = instance.LatestData;

            // Prepare the flow completion data
            var flowCompletedData = new FlowCompletedDataEto
            {
                InstanceId = instance.Id,
                Domain = workflow.Domain,
                Workflow = workflow.Key,
                Version = workflow.Version,
                CompletedState = instance.GetCurrentState,
                InstanceData = instance.IsSubFlow ? latestData?.Data.JsonElement : null,
                MetaData = instance.MetaData,
                CompletedAt = instance.CompletedAt ?? DateTime.UtcNow,
                Duration = instance.Duration
            };

            // Publish the event using Dapr pub/sub
            await daprClient.PublishEventAsync(
                configuration["DAPR_PUBSUB_STORE_NAME"],
                string.Format(DomainConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                flowCompletedData,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Successfully published flow completion event for instance {InstanceId}",
                instance.Id);

            // Record the event publication in metrics
            workflowMetrics.RecordDaprPubsubMessagePublished(configuration["DAPR_PUBSUB_STORE_NAME"],
                string.Format(DomainConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                "success");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish flow completion event for instance {InstanceId}",
                instance.Id);

            // Record the failure in metrics
            workflowMetrics.RecordDaprPubsubMessagePublished(configuration["DAPR_PUBSUB_STORE_NAME"],
                string.Format(DomainConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                "failed");
        }
    }
}
