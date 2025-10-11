using BBT.Aether.DistributedLock;
using BBT.Workflow.Domain.Extensions;
using BBT.Workflow.Execution.Strategies;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Service implementation for orchestrating workflow execution operations using the new pipeline architecture.
/// Coordinates between validation, preparation, handlers, and execution strategies.
/// </summary>
public sealed class WorkflowExecutionService(
    IExecutionStrategyFactory execFactory,
    ICurrentSchema currentSchema,
    IDistributedLockService distributedLockService,
    IInstanceRepository instanceRepository,
    ILogger<WorkflowExecutionService> logger) : IWorkflowExecutionService
{
    /// <summary>
    /// Executes a workflow transition using the new pipeline architecture.
    /// </summary>
    /// <param name="context">The workflow execution input containing all necessary data.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A service response containing the transition result.</returns>
    public async Task<InstanceServiceResponse<TransitionOutput>> ExecuteTransitionAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Starting transition execution for {TransitionKey} on instance {InstanceId} with trigger {TriggerType}",
            context.TransitionKey, context.InstanceId, context.TriggerType);

        using (currentSchema.Change(context.WorkflowKey))
        {
            var resourceId = $"instance-{context.InstanceId}";

            try
            {
                return await distributedLockService.ExecuteWithLockAsync(
                    resourceId,
                    InstanceConstants.TransitionLockExpiryInSeconds,
                    async () =>
                    {
                        var mode = execFactory.Get(context.Mode);
                        var transitionExecutionContext = await mode.ExecuteAsync(context, cancellationToken);
                        TransitionOutput? response = null;
                        if (transitionExecutionContext.ClientResponse is not null)
                        {
                            response = new TransitionOutput
                            {
                                Id = transitionExecutionContext.InstanceId,
                                Status = transitionExecutionContext.ClientResponse.Status
                            };
                        }
                        else
                        {
                            var freshInstance =
                                await instanceRepository.FindByIdAsReadOnlyAsync(context.InstanceId, cancellationToken);

                            response = new TransitionOutput
                            {
                                Id = context.InstanceId,
                                Status = freshInstance!.Status
                            };
                        }

                        return new InstanceServiceResponse<TransitionOutput>(response);
                    },
                    cancellationToken,
                    logger);
            }
            catch (DistributedLockAcquisitionException)
            {
                throw new TransitionLockedException(context.InstanceId, context.TransitionKey);
            }
        }
    }
}