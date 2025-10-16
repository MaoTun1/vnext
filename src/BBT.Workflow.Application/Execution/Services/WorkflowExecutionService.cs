using BBT.Aether.DistributedLock;
using BBT.Workflow.Domain;
using BBT.Workflow.Domain.Extensions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.Strategies;
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
    /// <returns>A Result containing the transition output.</returns>
    public async Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        using (currentSchema.Change(context.WorkflowKey))
        {
            // Enrich all logs with comprehensive workflow context for distributed tracing
            using (logger.ForTransition(
                domain: context.Domain,
                flow: context.WorkflowKey,
                flowVersion: context.WorkflowVersion,
                instanceId: context.InstanceId,
                transitionKey: context.TransitionKey))
            {
                // Get workflow info for structured logging (we'll get this after schema change)
                var resourceId = $"instance-{context.InstanceId}";

                // 1. Get execution strategy
                var strategyResult = execFactory.Get(context.Mode);
                if (!strategyResult.IsSuccess)
                    return Result<TransitionOutput>.Fail(strategyResult.Error);
                
                var strategy = strategyResult.Value!;
                
                // 2. Execute with distributed lock
                return await ResultExtensions.TryAsync<TransitionOutput>(async ct =>
                {
                    return await distributedLockService.ExecuteWithLockAsync(
                        resourceId,
                        InstanceConstants.TransitionLockExpiryInSeconds,
                        async () =>
                        {
                            // Execute strategy (now returns Result properly)
                            var executionResult = await strategy.ExecuteAsync(context, ct);
                            
                            // If strategy failed, propagate the error by throwing an exception
                            // that will be caught and converted to Result below
                            if (!executionResult.IsSuccess)
                            {
                                // For auto-transition condition not met, throw the special exception
                                if (executionResult.Error.Code == WorkflowErrorCodes.AutoTransitionConditionNotMet)
                                {
                                    throw new AutoTransitionConditionNotMetException(
                                        executionResult.Error.Code,
                                        executionResult.Error.Message ?? "Auto-transition condition not met");
                                }
                                
                                // For other errors, create a general exception with error info
                                throw new InvalidOperationException($"{executionResult.Error.Code}:{executionResult.Error.Message}");
                            }
                            
                            var transitionExecutionContext = executionResult.Value!;
                            
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
                                await instanceRepository.FindByIdAsReadOnlyAsync(context.InstanceId, ct);

                            response = new TransitionOutput
                            {
                                Id = context.InstanceId,
                                Status = freshInstance!.Status
                            };
                        }

                        return response;
                    },
                    ct,
                    logger);
                }, cancellationToken, ex => ex switch
                {
                    DistributedLockAcquisitionException => WorkflowErrors.TransitionLocked(context.InstanceId, context.TransitionKey),
                    
                    // Special handling for auto-transition condition not met
                    AutoTransitionConditionNotMetException atcEx => 
                        Error.Validation(atcEx.ErrorCode, atcEx.Message),
                    
                    // Special handling for auto-transition condition not met
                    WorkflowValidationException validationEx => Error.Validation(validationEx.ErrorCode, validationEx.Message, validationEx.ValidationErrors, validationEx.Target),
                    
                    InvalidOperationException ioe when ioe.Message.Contains(':') 
                        => Error.Failure(ioe.Message.Split(':')[0], ioe.Message.Split(':', 2)[1]),
                    
                    _ => Error.Failure(WorkflowErrorCodes.ExecutionStrategyFailed, $"Transition execution failed: {ex.Message}", ex.GetType().Name)
                });
            }
        }
    }
}