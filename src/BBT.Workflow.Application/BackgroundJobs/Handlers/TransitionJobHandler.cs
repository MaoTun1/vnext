using System.Text.Json;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles asynchronous transition background jobs.
/// This handler processes workflow transition requests that were submitted with Sync=true.
/// </summary>
public sealed class TransitionJobHandler(
    IJobStore jobStore,
    ICurrentSchema currentSchema,
    IWorkflowExecutionService workflowExecutionService,
    ILogger<TransitionJobHandler> logger) : IJobHandler
{
    public string JobName => BackgroundJobConsts.TransitionJobName;

    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var jobData = JsonSerializer.Deserialize<BackgroundJobInfo<TransitionJobPayload>>(jobPayload.Span);
        if (jobData == null)
        {
            logger.LogWarning("TransitionJobHandler: Failed to deserialize job payload.");
            return;
        }

        // Create structured logging scope for the entire job execution
        using (logger.ForJob(JobName, jobData.JobId))
        {
            logger.LogInformation("TransitionJobHandler: Processing job {JobName} - {JobId}", JobName, jobData.JobId);

            var flowName = jobData.GetFlowName();
            if (flowName.IsNullOrEmpty())
            {
                logger.LogWarning("TransitionJobHandler: Job Flow Name is empty for JobId {JobId}", jobData.JobId);
                return;
            }

            using (currentSchema.Change(flowName))
            {
                var jobInfo = await jobStore.GetAsync<TransitionJobPayload>(jobData.JobId, cancellationToken);
                if (jobInfo == null)
                {
                    logger.LogWarning("TransitionJobHandler: Job info not found for JobId {JobId}", jobData.JobId);
                    return;
                }

                // Mark job as triggered to prevent duplicate processing
                jobInfo.IsTriggered = true;
                await jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);

                try
                {
                    // For async processing, instance should already be pre-reserved and in Busy status
                    // Reconstruct the original TransitionInput with Sync=true
                    var transitionInput = new TransitionInput(
                            jobInfo.Payload.Domain,
                            jobInfo.Payload.Workflow,
                            jobInfo.Payload.Version,
                            jobInfo.Payload.Data,
                            sync: true) // Force sync=true to avoid infinite loop
                    {
                        Headers = jobInfo.Payload.Headers,
                        RouteValues = jobInfo.Payload.RouteValues
                    };

                    var context =
                        transitionInput.ToExecutionContext(jobInfo.Payload.InstanceId, jobInfo.Payload.TransitionKey);
                    context.Actor = jobInfo.Payload.ExecutionActor;

                    // Use the background-specific method that handles pre-reserved instances
                    await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);

                    logger.LogInformation(
                        "TransitionJobHandler: Successfully executed transition {TransitionKey} for instance {InstanceId}",
                        jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "TransitionJobHandler: Failed to execute transition for JobId {JobId}",
                        jobData.JobId);
                    throw;
                }
            }
        }
    }
}