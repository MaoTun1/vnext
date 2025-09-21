using System.Text.Json;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using BBT.Workflow.ExceptionHandling;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles asynchronous transition background jobs.
/// This handler processes workflow transition requests that were submitted with Sync=true.
/// </summary>
public sealed class TransitionJobHandler(
    IJobStore jobStore,
    ICurrentSchema currentSchema,
    IInstanceCommandAppService instanceCommandAppService,
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
                    RouteValues = jobInfo.Payload.RouteValues,
                    ExecutionContext = jobInfo.Payload.ExecutionContext
                };

                // Use the background-specific method that handles pre-reserved instances
                var result = await instanceCommandAppService.ExecuteBackgroundTransitionAsync(
                    jobInfo.Payload.InstanceId,
                    jobInfo.Payload.TransitionKey,
                    transitionInput,
                    cancellationToken);

                logger.LogInformation(
                    "TransitionJobHandler: Successfully executed transition {TransitionKey} for instance {InstanceId}",
                    jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId);
            }
            catch (TransitionRuleFailedException ex)
            {
                logger.LogWarning(ex, 
                    "TransitionJobHandler: Transition rule failed for JobId {JobId}: {Message}", 
                    jobData.JobId, ex.Message);
                // Don't rethrow for rule failures as they are expected business logic
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
