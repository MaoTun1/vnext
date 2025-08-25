using System.Text.Json;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Caching;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles workflow timeout jobs that are triggered when workflow instances exceed their configured timeout duration.
/// This handler is responsible for processing timeout events and transitioning workflow instances to their timeout state.
/// </summary>
/// <param name="jobStore">Store for retrieving and updating job information.</param>
/// <param name="instanceRepository">Repository for accessing and updating workflow instances.</param>
/// <param name="componentCacheStore">Cache store for retrieving workflow definitions and components.</param>
/// <param name="currentSchema">Service for managing schema context during multi-tenant operations.</param>
/// <param name="workflowMetrics">Service for recording workflow metrics including timeouts.</param>
/// <param name="runtimeInfoProvider">Provider for accessing runtime information such as domain context.</param>
/// <param name="logger">Logger instance for recording handler activities and errors.</param>
public sealed class FlowTimeoutJobHandler(
    IJobStore jobStore,
    IInstanceRepository instanceRepository,
    IComponentCacheStore componentCacheStore,
    ICurrentSchema currentSchema,
    IWorkflowMetrics workflowMetrics,
    IRuntimeInfoProvider runtimeInfoProvider,
    ILogger<FlowTimeoutJobHandler> logger
) : IJobHandler
{
    /// <summary>
    /// Gets the job name that this handler processes.
    /// This corresponds to the workflow timeout job constant.
    /// </summary>
    /// <value>The job name for workflow timeout jobs.</value>
    public string JobName => BackgroundJobConsts.FlowTimeoutJobName;

    /// <summary>
    /// Handles the execution of a workflow timeout job by processing the timeout event
    /// and transitioning the affected workflow instance to its timeout state.
    /// </summary>
    /// <param name="jobPayload">The serialized job payload containing timeout information.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous job processing operation.</returns>
    /// <remarks>
    /// This method performs the following operations:
    /// 1. Deserializes the job payload to extract timeout information
    /// 2. Retrieves the job information from the job store
    /// 3. Marks the job as triggered to prevent duplicate processing
    /// 4. Switches to the appropriate schema context for the workflow domain
    /// 5. Retrieves the workflow definition and instance data
    /// 6. Transitions the instance to the timeout state if it's still active
    /// 7. Completes the workflow instance and persists the changes
    /// 
    /// The handler gracefully handles various error conditions such as missing job data,
    /// workflow definitions, or instances, logging appropriate warnings for each case.
    /// Only active or busy workflow instances are affected by timeout processing.
    /// </remarks>
    /// <exception cref="JsonException">Thrown when the job payload cannot be deserialized.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required services are unavailable.</exception>
    public async Task HandleAsync(ReadOnlyMemory<byte> jobPayload, CancellationToken cancellationToken)
    {
        var jobData = JsonSerializer.Deserialize<BackgroundJobInfo<WorkflowTimeoutPayload>>(jobPayload.Span);
        if (jobData == null)
        {
            logger.LogWarning("FlowTimeoutJobHandler: Failed to deserialize job payload.");
            return;
        }

        logger.LogInformation("FlowTimeoutJobHandler: {JobName} - {JobId}", JobName, jobData?.JobId);

        var flowName = jobData?.GetFlowName();
        if (flowName.IsNullOrEmpty())
        {
            logger.LogWarning("AutoTransitionJobHandler: Job Flow Name is empty.");
            return;
        }

        using (currentSchema.Change(flowName))
        {
            var jobInfo = await jobStore.GetAsync<WorkflowTimeoutPayload>(jobData!.JobId, cancellationToken);
            if (jobInfo == null)
            {
                logger.LogWarning("FlowTimeoutJobHandler: Job info not found for JobId {JobId}", jobData.JobId);
                return;
            }

            jobInfo.IsTriggered = true;
            await jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);

            var workflow =
                await componentCacheStore.GetFlowAsync(jobInfo.Payload.Domain, jobInfo.Payload.FlowName,
                    jobInfo.Payload.Version, cancellationToken);

            var instance =
                await instanceRepository.FindAsync(p => p.Id == jobInfo.Payload.InstanceId, true, cancellationToken);
            if (instance == null)
            {
                logger.LogWarning("FlowTimeoutJobHandler: Instance not found with Id {InstanceId}",
                    jobInfo.Payload.InstanceId);
                return;
            }

            if (instance.Status.Equals(InstanceStatus.Active) || instance.Status.Equals(InstanceStatus.Busy))
            {
                // Record current status before timeout
                var currentStatus = instance.Status.Code;

                if (workflow.Timeout is null)
                {
                    logger.LogWarning("FlowTimeoutJobHandler: Timeout configuration missing for {Flow}", instance.Flow);
                    return;
                }

                instance.ChangeState(workflow.Timeout!);
                instance.Complete(); // This calculates the Duration

                // Record timeout metrics with duration - this will also decrement the current status gauge
                var durationSeconds = instance.Duration?.TotalSeconds;
                workflowMetrics.RecordInstanceTimedOut(instance.Flow, runtimeInfoProvider.Domain, currentStatus,
                    durationSeconds);
                await instanceRepository.UpdateAsync(instance, true, cancellationToken);
            }
        }
    }
}