using BBT.Aether.BackgroundJob;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Shared;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

public sealed class TransitionTimerJobHandler(
    IWorkflowExecutionService workflowExecutionService,
    IInstanceJobRepository jobRepository,
    ILogger<TransitionTimerJobHandler> logger) : IBackgroundJobHandler<TransitionTimerPayload>
{
    public const string HandlerName = "flow.transition.schedule";

    public async Task HandleAsync(TransitionTimerPayload args, CancellationToken cancellationToken)
    {
        try
        {
            var input = new TransitionInput(
                args.Domain,
                args.FlowName,
                args.Version
            );

            // Convert TransitionInput to WorkflowExecutionContext
            var executionContext = input.ToExecutionContext(
                args.InstanceId,
                args.TransitionKey);

            // Override trigger type to Scheduled for timer-based transitions
            executionContext.TriggerType = TriggerType.Scheduled;
            executionContext.Actor = ExecutionActor.System;
            executionContext.IsReentry = true; // Timer transitions are re-entry executions

            await workflowExecutionService.ExecuteTransitionAsync(
                executionContext,
                cancellationToken
            );

            await jobRepository.MarkAsProcessedAsync(args.JobName, cancellationToken);

            logger.LogInformation(
                "TransitionTimerJobHandler: Successfully executed transition {TransitionKey} for instance {InstanceId}",
                args.TransitionKey, args.InstanceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "TransitionTimerJobHandler: Transition execution cancelled for JobName {JobName}, TransitionKey {TransitionKey}, InstanceId {InstanceId}",
                args.JobName, args.TransitionKey, args.InstanceId);
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "TransitionTimerJobHandler: Unexpected error executing transition {TransitionKey} for instance {InstanceId}, JobName {JobName}",
                args.TransitionKey, args.InstanceId, args.JobName);
            
            throw;
        }
    }
}