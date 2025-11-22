using BBT.Aether.BackgroundJob;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Caching;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles workflow timeout jobs that are triggered when workflow instances exceed their configured timeout duration.
/// This handler is responsible for processing timeout events and transitioning workflow instances to their timeout state.
/// </summary>
public sealed class FlowTimeoutJobHandler(
    IInstanceRepository instanceRepository,
    IInstanceJobRepository jobRepository,
    IComponentCacheStore componentCacheStore,
    IWorkflowMetrics workflowMetrics,
    IRuntimeInfoProvider runtimeInfoProvider,
    ILogger<FlowTimeoutJobHandler> logger
) : IBackgroundJobHandler<WorkflowTimeoutPayload>
{
    public const string HandlerName = "flow.timeout";

    public async Task HandleAsync(WorkflowTimeoutPayload args, CancellationToken cancellationToken)
    {
        try
        {
            var workflow =
                await componentCacheStore.GetFlowAsync(args.Domain, args.FlowName,
                    args.Version, cancellationToken);

            var instance =
                await instanceRepository.FindAsync(p => p.Id == args.InstanceId, true,
                    cancellationToken);
            if (instance == null)
            {
                logger.LogWarning("FlowTimeoutJobHandler: Instance not found with Id {InstanceId}",
                    args.InstanceId);
                return;
            }

            if (instance.IsActive || instance.IsBusy)
            {
                // Record current status before timeout
                var currentStatus = instance.Status.Code;

                if (workflow.Timeout is null)
                {
                    logger.LogWarning("FlowTimeoutJobHandler: Timeout configuration missing for {Flow}",
                        instance.Flow);
                    return;
                }

                instance.ChangeState(workflow.Timeout!);
                instance.Complete(); // This calculates the Duration

                // Record timeout metrics with duration - this will also decrement the current status gauge
                var durationSeconds = instance.Duration?.TotalSeconds;
                workflowMetrics.RecordInstanceTimedOut(instance.Flow, runtimeInfoProvider.Domain, currentStatus,
                    durationSeconds);
                await instanceRepository.UpdateAsync(instance, true, cancellationToken);
                await jobRepository.MarkAsProcessedAsync(args.JobName, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "TransitionJobHandler: Failed to execute transition for JobName {JobId}",
                args.JobName);
            throw;
        }
    }
}