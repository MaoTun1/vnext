using System.Text.Json;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using BBT.Workflow.Shared;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

public sealed class TransitionTimerJobHandler(
    IJobStore jobStore,
    IWorkflowExecutionService workflowExecutionService,
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

        // Create structured logging scope for the entire job execution
        using var scope = logger.ForJob(JobName, jobData.JobId);
        
        logger.LogInformation("TransitionTimerJobHandler: {JobName} - {JobId}", JobName, jobData.JobId);

        var flowName = jobData.GetFlowName();
        if (flowName.IsNullOrEmpty())
        {
            logger.LogWarning("TransitionTimerJobHandler: Job Flow Name is empty for JobId {JobId}", jobData.JobId);
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

            // Check if job was already triggered to avoid duplicate execution
            if (jobInfo.IsTriggered)
            {
                logger.LogWarning("TransitionTimerJobHandler: Job {JobId} was already triggered, skipping execution", jobData.JobId);
                return;
            }

            // Mark job as triggered before execution to prevent race conditions
            jobInfo.IsTriggered = true;
            await jobStore.SaveAsync(jobInfo.JobId, jobInfo, cancellationToken);

            logger.LogDebug("TransitionTimerJobHandler: Starting execution of transition {TransitionKey} for instance {InstanceId}",
                jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId);

            try
            {
                var input = new TransitionInput(
                    jobInfo.Payload.Domain,
                    jobInfo.Payload.FlowName,
                    jobInfo.Payload.Version
                );

                // Convert TransitionInput to WorkflowExecutionContext
                var executionContext = input.ToExecutionContext(
                    jobInfo.Payload.InstanceId,
                    jobInfo.Payload.TransitionKey);

                // Override trigger type to Scheduled for timer-based transitions
                executionContext.TriggerType = TriggerType.Scheduled;
                executionContext.Actor = ExecutionActor.System;
                executionContext.IsReentry = true; // Timer transitions are re-entry executions

                await workflowExecutionService.ExecuteTransitionAsync(
                    executionContext,
                    cancellationToken
                );

                logger.LogInformation(
                    "TransitionTimerJobHandler: Successfully executed transition {TransitionKey} for instance {InstanceId}",
                    jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "TransitionTimerJobHandler: Transition execution cancelled for JobId {JobId}, TransitionKey {TransitionKey}, InstanceId {InstanceId}",
                    jobData.JobId, jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId);
                throw; // Re-throw cancellation exceptions
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "TransitionTimerJobHandler: Unexpected error executing transition {TransitionKey} for instance {InstanceId}, JobId {JobId}",
                    jobInfo.Payload.TransitionKey, jobInfo.Payload.InstanceId, jobData.JobId);
                
                // For unexpected errors, we might want to mark the job as failed
                // or implement retry logic depending on business requirements
                throw;
            }
        }
    }
}