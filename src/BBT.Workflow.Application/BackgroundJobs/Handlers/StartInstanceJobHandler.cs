using System.Text.Json;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles asynchronous start instance background jobs.
/// This handler processes workflow instance creation requests that were submitted with Sync=true.
/// </summary>
public sealed class StartInstanceJobHandler(
    IJobStore jobStore,
    ICurrentSchema currentSchema,
    IInstanceCommandAppService instanceCommandAppService,
    ILogger<StartInstanceJobHandler> logger) : IJobHandler
{
    public string JobName => BackgroundJobConsts.StartInstanceJobName;

    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var jobData = JsonSerializer.Deserialize<BackgroundJobInfo<StartInstanceJobPayload>>(jobPayload.Span);
        if (jobData == null)
        {
            logger.LogWarning("StartInstanceJobHandler: Failed to deserialize job payload.");
            return;
        }

        logger.LogInformation("StartInstanceJobHandler: Processing job {JobName} - {JobId}", JobName, jobData.JobId);

        var flowName = jobData.GetFlowName();
        if (flowName.IsNullOrEmpty())
        {
            logger.LogWarning("StartInstanceJobHandler: Job Flow Name is empty for JobId {JobId}", jobData.JobId);
            return;
        }
        
        using (currentSchema.Change(flowName))
        {
            var jobInfo = await jobStore.GetAsync<StartInstanceJobPayload>(jobData.JobId, cancellationToken);
            if (jobInfo == null)
            {
                logger.LogWarning("StartInstanceJobHandler: Job info not found for JobId {JobId}", jobData.JobId);
                return;
            }

            // Mark job as triggered to prevent duplicate processing
            jobInfo.IsTriggered = true;
            await jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);

            try
            {
                // Reconstruct the original StartInstanceInput with Sync=false
                var startInput = new StartInstanceInput(
                    jobInfo.Payload.Domain,
                    jobInfo.Payload.Workflow,
                    jobInfo.Payload.Version,
                    sync: true) // Force sync=true to avoid infinite loop
                {
                    Instance = new CreateInstanceInput
                    {
                        Id = jobInfo.Payload.InstanceId,
                        Key = jobInfo.Payload.InstanceKey,
                        Tags = jobInfo.Payload.Tags,
                        Attributes = jobInfo.Payload.Attributes,
                        Callback = jobInfo.Payload.Callback,
                        MetaData = new ObjectDictionary(jobInfo.Payload.MetaData)
                    },
                    Headers = jobInfo.Payload.Headers,
                    RouteValues = jobInfo.Payload.RouteValues
                };

                // Use the existing service method - much cleaner!
                var result = await instanceCommandAppService.StartAsync(startInput, cancellationToken);

                logger.LogInformation(
                    "StartInstanceJobHandler: Successfully started instance {InstanceId} for workflow {Workflow}",
                    result.Data.Id, jobInfo.Payload.Workflow);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, 
                    "StartInstanceJobHandler: Failed to start instance for JobId {JobId}", 
                    jobData.JobId);
                throw;
            }
        }
    }
}
