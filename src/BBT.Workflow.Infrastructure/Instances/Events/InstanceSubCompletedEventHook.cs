using BBT.Aether.MultiSchema;
using BBT.Aether.Uow;
using BBT.Workflow.Events.Hooks;
using BBT.Workflow.Instances.Remote;
using BBT.Workflow.Logging;
using BBT.Workflow.Runtime;
using BBT.Workflow.SubFlow;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Instances.Events;

/// <summary>
/// Hook executed before InstanceSubCompletedEvent is published.
/// Performs pre-publish processing for sub-flow completion events.
/// This hook delegates the actual completion logic to ISubflowCompletionService.
/// </summary>
/// <remarks>
/// Register this hook in DI using:
/// <code>
/// services.AddEventHook&lt;InstanceSubCompletedEvent, InstanceSubCompletedEventHook&gt;();
/// </code>
/// </remarks>
public sealed class InstanceSubCompletedEventHook(
    ILogger<InstanceSubCompletedEventHook> logger,
    IRemoteInstanceCommandAppService remoteInstanceCommandAppService,
    ISubflowCompletionService subflowCompletionService,
    IRuntimeInfoProvider runtimeInfoProvider,
    ICurrentSchema currentSchema,
    IUnitOfWorkManager unitOfWorkManager) : IEventPublishHook<InstanceSubCompletedEvent>
{
    /// <summary>
    /// Executes hook logic before the InstanceSubCompletedEvent is published.
    /// Routes to local or remote processing based on domain match.
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
        logger.SubFlowEventReceived(eventData.SubInstanceId, eventData.InstanceId, eventData.Domain, eventData.Flow);
        
        try
        {
            var input = MapToFlowCompletedInput(eventData);

            if (runtimeInfoProvider.IsDomainMatch(eventData.Domain))
            {
                await ProcessLocalAsync(input, cancellationToken);
            }
            else
            {
                await remoteInstanceCommandAppService.CompleteAsync(input, cancellationToken);
            }

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

    /// <summary>
    /// Processes the subflow completion locally with proper schema and UoW scope.
    /// </summary>
    private async Task ProcessLocalAsync(FlowCompletedInput input, CancellationToken cancellationToken)
    {
        using (currentSchema.Use(input.Flow))
        {
            await using (var uow = await unitOfWorkManager.BeginAsync(new UnitOfWorkOptions
                         {
                             Scope = UnitOfWorkScopeOption.RequiresNew
                         }, cancellationToken))
            {
                await subflowCompletionService.CompletionAsync(input, cancellationToken);
                await uow.SaveChangesAsync(cancellationToken);
                await uow.CommitAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Maps the event data to FlowCompletedInput DTO.
    /// </summary>
    private static FlowCompletedInput MapToFlowCompletedInput(InstanceSubCompletedEvent eventData)
    {
        return new FlowCompletedInput
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
        };
    }
}
