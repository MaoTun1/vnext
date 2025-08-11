using System.Text.Json;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

public sealed class TransitionTimerJobHandler(
    IJobStore jobStore,
    IInstanceCommandAppService instanceAppService,
    ICurrentSchema currentSchema,
    ILogger<TransitionTimerJobHandler> logger) : IJobHandler
{
    public string JobName => BackgroundJobConsts.TransitionTimerJobName;

    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var jobData = JsonSerializer.Deserialize<BackgroundJobInfo<TransitionTimerPayload>>(jobPayload.Span);
        if (jobData == null)
        {
            logger.LogWarning("TransitionTimerJobHandler: Failed to deserialize job payload.");
            return;
        }

        logger.LogInformation("TransitionTimerJobHandler: {JobName} - {JobId}", JobName, jobData.JobId);

        var flowName = jobData.GetFlowName();
        if (flowName.IsNullOrEmpty())
        {
            logger.LogWarning("AutoTransitionJobHandler: Job Flow Name is empty.");
            return;
        }

        using (currentSchema.Change(flowName))
        {
            var jobInfo = await jobStore.GetAsync<TransitionTimerPayload>(jobData.JobId, cancellationToken);
            if (jobInfo == null)
            {
                logger.LogWarning("TransitionTimerJobHandler: Job info not found for JobId {JobId}", jobData.JobId);
                return;
            }

            jobInfo.IsTriggered = true;
            await jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);

            try
            {
                await instanceAppService.TransitionAsync(
                    jobInfo.Payload.InstanceId,
                    jobInfo.Payload.TransitionKey,
                    new TransitionInput(
                        jobInfo.Payload.Domain,
                        jobInfo.Payload.FlowName,
                        jobInfo.Payload.Version
                    ),
                    cancellationToken
                );

                logger.LogInformation(
                    "TransitionTimerJobHandler: Successfully executed transition {TransitionKey} for instance {InstanceId}",
                    jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId);
            }
            catch (TransitionRuleFailedException e)
            {
                logger.LogWarning(
                    "TransitionTimerJobHandler: Transition rule failed for JobId {JobId}, Reason: {Reason}",
                    jobData.JobId, e.Message);
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "TransitionTimerJobHandler: Error executing transition {TransitionKey} for instance {InstanceId}",
                    jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId);
            }
        }
    }
}