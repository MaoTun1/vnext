using BBT.Aether.Events;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Events;
using BBT.Workflow.Instances.Remote;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Workers.Inbox.Handlers;

/// <summary>
/// Handles ChildSubflowCancelRequestedEvent to propagate cancellation to child subflows.
/// </summary>
internal sealed class ChildSubflowCancelRequestedEventHandler(
    IRemoteInstanceCommandAppService commandAppService,
    ILogger<ChildSubflowCancelRequestedEventHandler> logger
) : IEventHandler<ChildSubflowCancelRequestedEvent>
{
    /// <summary>
    /// Handles the ChildSubflowCancelRequestedEvent by transitioning the child subflow to cancel state.
    /// </summary>
    public async Task HandleAsync(CloudEventEnvelope<ChildSubflowCancelRequestedEvent> envelope,
        CancellationToken cancellationToken)
    {
        var eventData = envelope.Data;

        logger.ChildSubflowCancelRequestReceived(
            eventData.InstanceId,
            eventData.Domain,
            eventData.Flow);

        try
        {
            var result = await commandAppService.TransitionAsync(
                eventData.InstanceId,
                WellKnownTransitionKeys.Cancel,
                new TransitionInput(
                    domain: eventData.Domain,
                    workflow: eventData.Flow,
                    version: eventData.Version),
                cancellationToken: cancellationToken);

            if (result.IsSuccess)
            {
                logger.ChildSubflowCancelSucceeded(eventData.InstanceId);
                return;
            }

            logger.ChildSubflowCancelFailed(eventData.InstanceId);
        }
        catch (Exception ex)
        {
            logger.ChildSubflowCancelError(ex, eventData.InstanceId);
        }
    }
}