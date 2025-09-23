using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Headers;
using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Http;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.Logging;
using ExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Implements synchronous transition execution strategy.
/// Executes the workflow transition directly with distributed locking.
/// </summary>
public sealed class SyncTransitionStrategy(
    IStateMachineExecutor stateMachineExecutor,
    IInstanceRepository instanceRepository,
    IHeaderService headerService,
    IRuntimeInfoProvider runtimeInfoProvider,
    ILogger<SyncTransitionStrategy> logger) : ITransitionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(bool isSync) => isSync;

    /// <inheritdoc />
    public async Task<InstanceServiceResponse<TransitionOutput>> ExecuteAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Executing synchronous transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        var instance = await ExecuteTransitionWithinLockAsync(context, cancellationToken);

        // Add workflow information to response headers
        headerService.AddHeader(
            WorkflowInfo.Name,
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, context.Workflow.Key,
                context.Workflow.Version, instance.Id)
        );

        return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
        {
            Id = instance.Id,
            Status = instance.Status
        });
    }

    /// <summary>
    /// Executes the transition logic within the distributed lock context.
    /// </summary>
    private async Task<Instance> ExecuteTransitionWithinLockAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithBusyStatusAsync(context.Instance, context.Input.ExecutionContext, async () =>
        {
            var scriptContext = await context.ScriptContextBuilder.BuildAsync(cancellationToken);

            // Delegate to StateMachineExecutor for actual transition execution
            await stateMachineExecutor.ExecuteTransitionAsync(scriptContext, cancellationToken);

            return context.Instance;
        }, cancellationToken);

        return context.Instance;
    }

    /// <summary>
    /// Handles busy status transitions with proper cleanup.
    /// </summary>
    private async Task ExecuteWithBusyStatusAsync<T>(Instance instance,
        ExecutionContext executionContext,
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (executionContext == ExecutionContext.User && instance.Status.Equals(InstanceStatus.Active))
        {
            // Set instance to busy
            instance.Busy();
            await instanceRepository.UpdateAsync(instance, true, cancellationToken);
        }

        try
        {
            // Execute the operation
            await operation();
        }
        finally
        {
            if (executionContext == ExecutionContext.User)
            {
                // Always reset to active, even on failure
                try
                {
                    if (!instance.Status.Equals(InstanceStatus.Completed))
                    {
                        instance.Active();
                        await instanceRepository.UpdateAsync(instance, true, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't throw - we don't want to mask the original exception
                    logger.LogWarning(ex,
                        "Failed to reset instance {InstanceId} status to Active after transition processing",
                        instance.Id);
                }
            }
        }
    }
}