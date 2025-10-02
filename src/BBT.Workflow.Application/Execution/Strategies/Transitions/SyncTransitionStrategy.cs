using BBT.Aether.Guids;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Headers;
using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Http;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.Logging;
using ExecutionContext = BBT.Workflow.Shared.ExecutionContext;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Implements synchronous transition execution strategy.
/// Executes the workflow transition directly with distributed locking.
/// </summary>
public sealed class SyncTransitionStrategy(
    IStateMachineExecutor stateMachineExecutor,
    IInstanceRefreshStrategy instanceRefreshStrategy,
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

        var refreshedInstance = await instanceRefreshStrategy.GetLatestInstanceAsync(context.InstanceId, cancellationToken);

        // Add workflow information to response headers
        headerService.AddHeader(
            WorkflowInfo.Name,
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, context.Workflow.Key,
                context.Workflow.Version, refreshedInstance.Id)
        );

        return new InstanceServiceResponse<TransitionOutput>(new TransitionOutput
        {
            Id = refreshedInstance.Id,
            Status = refreshedInstance.Status
        });
    }
}