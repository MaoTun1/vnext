using BBT.Aether.BackgroundJob;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJobs.Handlers;

/// <summary>
/// Handles asynchronous transition background jobs.
/// This handler processes workflow transition requests that were submitted with Sync=true.
/// </summary>
public sealed class TransitionJobHandler(
    IInstanceJobRepository jobRepository,
    IWorkflowExecutionService workflowExecutionService,
    ILogger<TransitionJobHandler> logger) : IBackgroundJobHandler<TransitionJobPayload>
{
    public const string HandlerName = "flow.transition";

    public async Task HandleAsync(TransitionJobPayload args, CancellationToken cancellationToken)
    {
        try
        {
            // For async processing, instance should already be pre-reserved and in Busy status
            // Reconstruct the original TransitionInput with Sync=true
            var transitionInput = new TransitionInput(
                    args.Domain,
                    args.Workflow,
                    args.Version,
                    new TransitionDataInput(args.Data)
                    {
                        Key = args.InstanceKey,
                        Tags = args.Tags
                    },
                    sync: true) // Force sync=true to avoid infinite loop
                {
                    Headers = args.Headers,
                    RouteValues = args.RouteValues
                };

            var context =
                transitionInput.ToExecutionContext(args.InstanceId.ToString(), args.TransitionKey);
            context.Actor = args.ExecutionActor;

            // Use the background-specific method that handles pre-reserved instances
            var result = await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);
            await jobRepository.MarkAsProcessedAsync(args.JobName, cancellationToken);
            logger.JobCompleted(args.JobName, args.TransitionKey, args.InstanceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.JobCancelled(args.JobName, args.TransitionKey, args.InstanceId);
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception e)
        {
            logger.JobFailed(e, args.JobName, args.InstanceId);
            throw;
        }
    }
}