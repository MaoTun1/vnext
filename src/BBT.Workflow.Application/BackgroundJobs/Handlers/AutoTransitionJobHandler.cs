using System.Text.Json;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

public sealed class AutoTransitionJobHandler(
    IJobStore jobStore,
    IInstanceCommandAppService instanceAppService,
    ICurrentSchema currentSchema,
    ILogger<FlowTimeoutJobHandler> logger) : IJobHandler
{
    public string JobName => BackgroundJobConsts.AutoTransitionJobName;

    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var jobData = JsonSerializer.Deserialize<BackgroundJobInfo<AutoTransitionPayload>>(jobPayload.Span);
        if (jobData == null)
        {
            logger.LogWarning("AutoTransitionJobHandler: Failed to deserialize job payload.");
            return;
        }

        logger.LogInformation("AutoTransitionJobHandler: {JobName} - {JobId}", JobName, jobData.JobId);

        var flowName = jobData.GetFlowName();
        if (flowName.IsNullOrEmpty())
        {
            logger.LogWarning("AutoTransitionJobHandler: Job Flow Name is empty.");
            return;
        }
        
        using (currentSchema.Change(flowName))
        {
            var jobInfo = await jobStore.GetAsync<AutoTransitionPayload>(jobData.JobId, cancellationToken);
            if (jobInfo == null)
            {
                logger.LogWarning("AutoTransitionJobHandler: Job info not found for JobId {JobId}", jobData.JobId);
                return;
            }

            jobInfo.IsTriggered = true;
            await jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);
        
            foreach (var transitionKey in jobInfo.Payload.TransitionKeys)
            {
                try
                {
                    var input = new TransitionInput(
                        jobInfo.Payload.Domain,
                        jobInfo.Payload.FlowName,
                        jobInfo.Payload.Version
                    )
                    {
                        ExecutionContext = WorkflowExecutionContext.System // System context for auto transitions
                    };

                    await instanceAppService.TransitionAsync(
                        jobInfo.Payload.InstanceId,
                        transitionKey,
                        input,
                        cancellationToken
                    );
                    break;
                }
                catch (TransitionRuleFailedException e)
                {
                    logger.LogWarning("AutoTransitionJobHandler: {Reason} for JobId {JobId}", jobData.JobId, e.Message);
                }
            }
        }
    }
}