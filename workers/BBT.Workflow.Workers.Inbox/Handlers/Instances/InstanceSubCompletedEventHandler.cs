using BBT.Aether.Events;
using BBT.Workflow.Instances.Events;
using BBT.Workflow.Logging;
using BBT.Workflow.SubFlow;

namespace BBT.Workflow.Workers.Inbox.Handlers;

/// <summary>
/// Handles the InstanceSubCompletedEvent to process SubFlow/SubProcess completion.
/// This handler is invoked via the Inbox pattern when a SubItem completes.
/// </summary>
internal sealed class InstanceSubCompletedEventHandler(
    ISubflowCompletionService subflowCompletionService,
    ILogger<InstanceSubCompletedEventHandler> logger) : IEventHandler<InstanceSubCompletedEvent>
{
    public async Task HandleAsync(CloudEventEnvelope<InstanceSubCompletedEvent> envelope, CancellationToken cancellationToken)
    {
        var eventData = envelope.Data;
        
        logger.SubFlowEventReceived(
            TelemetryConstants.Prefixes.Application,
            eventData.SubInstanceId,
            eventData.InstanceId,
            eventData.Domain,
            eventData.Flow);

        try
        {
            // Map event to FlowCompletedInput for processing
            var completedData = new FlowCompletedInput
            {
                SubInstanceId = eventData.SubInstanceId,
                InstanceId = eventData.InstanceId,
                Domain = eventData.Domain,
                Flow = eventData.Flow,
                Version = eventData.Version,
                CompletedState = eventData.CompletedState,
                InstanceData = eventData.InstanceData,
                CompletedAt = eventData.CompletedAt,
                Duration = eventData.Duration
            };

            await subflowCompletionService.CompletionAsync(completedData, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.SubFlowCompletionFailed(
                ex,
                TelemetryConstants.Prefixes.Application,
                eventData.SubInstanceId,
                eventData.InstanceId);
            
            throw;
        }
    }
}