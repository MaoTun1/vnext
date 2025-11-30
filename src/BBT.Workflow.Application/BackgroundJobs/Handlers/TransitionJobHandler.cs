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
                    args.Data,
                    sync: true) // Force sync=true to avoid infinite loop
                {
                    Headers = args.Headers,
                    RouteValues = args.RouteValues
                };

            var context =
                transitionInput.ToExecutionContext(args.InstanceId, args.TransitionKey);
            context.Actor = args.ExecutionActor;

            // Use the background-specific method that handles pre-reserved instances
            await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);

            await jobRepository.MarkAsProcessedAsync(args.JobName, cancellationToken);
            
            logger.JobCompleted(args.JobName, args.TransitionKey, args.InstanceId);
        }
        catch (Exception ex)
        {
            logger.JobFailed(ex, args.JobName, args.InstanceId);
        }
    }
}