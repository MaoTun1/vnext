using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Dapr.Jobs.Models;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Implements asynchronous instance start execution strategy.
/// Enqueues the workflow instance start operation as a background job.
/// </summary>
public sealed class AsyncInstanceStartStrategy(
    IBackgroundJobService backgroundJobService,
    ICurrentSchema currentSchema,
    IInstanceRepository instanceRepository,
    IStateMachineExecutor stateMachineExecutor,
    ILogger<AsyncInstanceStartStrategy> logger) : IInstanceStartStrategy
{
    /// <inheritdoc />
    public bool CanHandle(bool isSync) => !isSync;

    /// <inheritdoc />
    public async Task<InstanceServiceResponse<StartInstanceOutput>> ExecuteAsync(
        InstanceStartExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Executing asynchronous start for instance {InstanceId}", context.Instance.Id);

        using (currentSchema.Change(context.Input.Workflow))
        {
            // Schedule flow timeout if configured
            await stateMachineExecutor.FlowTimeoutAsync(context.Workflow, context.Instance, cancellationToken);
            
            context.Instance.Busy();
            await instanceRepository.UpdateStatusAsync(context.Instance, cancellationToken);

            var jobId = $"start-instance-{context.Instance.Id}";

            // Create job payload
            var payload = new StartInstanceJobPayload
            {
                Domain = context.Input.Domain,
                Workflow = context.Input.Workflow,
                Version = context.Input.Version,
                InstanceId = context.Instance.Id,
                InstanceKey = context.Input.Instance.Key,
                TransitionKey = context.Workflow.StartTransition.Key,
                Tags = context.Input.Instance.Tags,
                Attributes = context.Input.Instance.Attributes,
                Callback = context.Input.Instance.Callback,
                MetaData = context.Input.Instance.MetaData.ToDictionary(kvp => kvp.Key,
                    kvp => kvp.Value ?? string.Empty),
                Headers = context.Input.Headers,
                RouteValues = context.Input.RouteValues
            };

            // Ensure sync metadata is set
            payload.MetaData.TryAdd(DomainConsts.MetaDataKeys.Sync, context.Input.Sync.ToString().ToLower());
            payload.MetaData.TryAdd(DomainConsts.MetaDataKeys.Callback, context.Input.Instance.Callback ?? string.Empty);

            // Create job metadata
            var jobMetadata = new Dictionary<string, string>
            {
                ["domain"] = context.Input.Domain,
                ["flowName"] = context.Input.Workflow,
                ["instanceId"] = context.Instance.Id.ToString()
            };

            // Schedule job to run immediately
            var schedule = DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(1));

            await backgroundJobService.EnqueueAsync(
                BackgroundJobConsts.StartInstanceJobName,
                jobId,
                schedule,
                payload,
                jobMetadata,
                cancellationToken);

            logger.LogInformation(
                "Enqueued start instance job {JobId} for workflow {Workflow} with instance {InstanceId}",
                jobId, context.Input.Workflow, context.Instance.Id);

            // Return response with instance ID and Busy status to indicate background processing
            return new InstanceServiceResponse<StartInstanceOutput>(new StartInstanceOutput
            {
                Id = context.Instance.Id,
                Status = InstanceStatus.Busy // Indicate that the instance is being processed in background
            });
        }
    }
}
