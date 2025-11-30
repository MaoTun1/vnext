using BBT.Aether.BackgroundJob;
using BBT.Aether.Events;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Events;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Workers.Inbox.Handlers;

/// <summary>
/// Handles InstanceCanceledEvent to propagate cancellation to child flows and cancel active jobs.
/// Implements the cancellation behavior for workflow instances.
/// </summary>
internal sealed class InstanceCanceledEventHandler(
    IInstanceRepository instanceRepository,
    IInstanceJobRepository instanceJobRepository,
    IBackgroundJobService backgroundJobService,
    ILogger<InstanceCanceledEventHandler> logger) : IEventHandler<InstanceCanceledEvent>
{
    /// <summary>
    /// Handles the InstanceCanceledEvent by performing cancellation cleanup.
    /// </summary>
    public async Task HandleAsync(CloudEventEnvelope<InstanceCanceledEvent> envelope,
        CancellationToken cancellationToken)
    {
        var eventData = envelope.Data;

        logger.InstanceCanceledEventReceived(
            eventData.InstanceId,
            eventData.Flow);

        try
        {
            var instance = await instanceRepository.FindAsync(eventData.InstanceId, true, cancellationToken);

            if (instance == null)
                return;

            var jobs = await instanceJobRepository.GetListActiveAsync(instance.Id, cancellationToken);
            if (jobs.Any())
            {
                // Process all jobs in parallel for better performance
                var processingTasks = jobs.Select(async job =>
                {
                    await backgroundJobService.DeleteAsync(job.JobId, cancellationToken);
                    job.MarkAsProcessed();
                    await instanceJobRepository.UpdateAsync(job, true, cancellationToken);
                });

                await Task.WhenAll(processingTasks);

                logger.InstanceCanceledJobsProcessed(eventData.InstanceId, jobs.Count);
            }
        }
        catch (Exception ex)
        {
            logger.InstanceCanceledProcessingFailed(ex, eventData.InstanceId);
        }
    }
}