using BBT.Aether.Guids;
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
    IGuidGenerator guidGenerator,
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
        var updatedInstance = await ExecuteWithBusyStatusAsync(context.Instance, context.Input.ExecutionContext,
            async () =>
            {
                if (context.Input.Data.HasValue)
                {
                    context.Instance.AddData(
                        guidGenerator.Create(),
                        new JsonData(context.Input.Data),
                        context.Transition.VersionStrategy
                    );
                }

                var scriptContext = await context.ScriptContextBuilder.BuildAsync(cancellationToken);

                // Delegate to StateMachineExecutor for actual transition execution
                await stateMachineExecutor.ExecuteTransitionAsync(scriptContext, cancellationToken);

                return context.Instance;
            }, cancellationToken);

        // Return the updated instance to ensure caller gets the latest state
        return updatedInstance;
    }

    /// <summary>
    /// Handles busy status transitions with proper cleanup.
    /// Returns the updated instance with latest correlations.
    /// </summary>
    private async Task<Instance> ExecuteWithBusyStatusAsync<T>(Instance instance,
        ExecutionContext executionContext,
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (executionContext == ExecutionContext.User && instance.IsActive)
        {
            // Set instance to busy
            instance.Busy();
            await instanceRepository.UpdateStatusAsync(instance, cancellationToken);
        }

        // Execute the operation
        await operation();

        // Return the fresh instance from database to ensure caller gets the latest correlations
        return instance;
        //await instanceRepository.GetAsync(instance.Id, true, cancellationToken);
    }
}