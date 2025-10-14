using BBT.Aether.DistributedLock;
using BBT.Workflow.Domain.Extensions;
using BBT.Workflow.Execution.Strategies;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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
        var sw = Stopwatch.StartNew();
        
        using (currentSchema.Change(context.WorkflowKey))
        {
            // Get workflow info for structured logging (we'll get this after schema change)
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
                        
                        sw.Stop();
                        
                        // Log completion with structured data
                        logger.TransitionCompleted(
                            TelemetryConstants.Prefixes.Application,
                            context.TransitionKey,
                            context.InstanceId,
                            sw.ElapsedMilliseconds);
                        
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
            catch (Exception ex)
            {
                sw.Stop();
                logger.TransitionFailed(ex, TelemetryConstants.Prefixes.Application, context.TransitionKey, context.InstanceId);
                throw;
            }
        }
    }
}