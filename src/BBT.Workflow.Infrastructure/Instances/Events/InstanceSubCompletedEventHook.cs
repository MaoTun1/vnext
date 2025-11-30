using BBT.Workflow.Events.Hooks;
using BBT.Workflow.Instances.Remote;
using BBT.Workflow.Logging;
using BBT.Workflow.SubFlow;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Instances.Events;

/// <summary>
/// Hook executed before InstanceSubCompletedEvent is published.
/// Performs pre-publish processing for sub-flow completion events.
/// </summary>
/// <remarks>
/// Register this hook in DI using:
/// <code>
/// services.AddEventHook&lt;InstanceSubCompletedEvent, InstanceSubCompletedEventHook&gt;();
/// </code>
/// </remarks>
public sealed class InstanceSubCompletedEventHook(
    ILogger<InstanceSubCompletedEventHook> logger,
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService) : IEventPublishHook<InstanceSubCompletedEvent>
{
    /// <summary>
    /// Executes hook logic before the InstanceSubCompletedEvent is published.
    /// </summary>
    /// <param name="eventData">The strongly-typed event data.</param>
    /// <param name="context">The hook context with metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook execution result.</returns>
    public async Task<EventHookResult> BeforePublishAsync(
        InstanceSubCompletedEvent eventData,
        EventHookContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await remoteInstanceCommandAppService.CompleteAsync(new FlowCompletedInput
            {
                InstanceId = eventData.InstanceId,
                Domain = eventData.Domain,
                Flow = eventData.Flow,
                CompletedAt = eventData.CompletedAt,
                CompletedState = eventData.CompletedState,
                Duration = eventData.Duration,
                SubInstanceId = eventData.SubInstanceId,
                InstanceData = eventData.InstanceData,
                Version = eventData.Version
            }, cancellationToken);

            return EventHookResult.Ok(new Dictionary<string, string>
            {
                ["hook_executed"] = "true",
                ["sub_instance_id"] = eventData.SubInstanceId.ToString(),
                ["parent_instance_id"] = eventData.InstanceId.ToString()
            });
        }
        catch (Exception ex)
        {
            logger.SubFlowCompletionFailed(ex, eventData.SubInstanceId, eventData.InstanceId);

            return EventHookResult.Fail(ex, new Dictionary<string, string>
            {
                ["hook_error"] = "SubFlowCompletionHookFailed"
            });
        }
    }
}