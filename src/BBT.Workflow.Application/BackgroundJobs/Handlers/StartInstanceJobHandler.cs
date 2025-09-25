using System.Text.Json;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.Logging;
using ExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles asynchronous start instance background jobs.
/// This handler processes workflow instance creation requests that were submitted with Sync=true.
/// </summary>
public sealed class StartInstanceJobHandler(
    IJobStore jobStore,
    ICurrentSchema currentSchema,
    IWorkflowExecutionService workflowExecutionService,
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
                // For async processing, instance should already be pre-created and in Busy status
                // We need to use the StateMachineExecutor directly to handle the pre-created instance properly

                // Reconstruct the original StartInstanceInput with Sync=true
                var transitionInput = new TransitionInput(
                        jobInfo.Payload.Domain,
                        jobInfo.Payload.Workflow,
                        jobInfo.Payload.Version,
                        jobInfo.Payload.Attributes,
                        sync: true) // Force sync=true to avoid infinite loop
                    {
                        Headers = jobInfo.Payload.Headers,
                        RouteValues = jobInfo.Payload.RouteValues,
                        ExecutionContext = ExecutionContext.User
                    };

                // Use the background-specific method that handles pre-created instances
                var result = await workflowExecutionService.ExecuteTransitionAsync(
                    jobInfo.Payload.InstanceId!.Value,
                    jobInfo.Payload.TransitionKey,
                    transitionInput,
                    cancellationToken);

                logger.LogInformation(
                    "StartInstanceJobHandler: Successfully started instance {InstanceId} for workflow {Workflow}",
                    result.Data.Id, jobInfo.Payload.Workflow);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "StartInstanceJobHandler: Failed to start instance for JobId {JobId}",
                    jobData.JobId);

                // TODO: An error occurred while starting the instance. How should the system react?
                // TODO: Error details and retry feature should be added to JobInfo
            }
        }
    }
}