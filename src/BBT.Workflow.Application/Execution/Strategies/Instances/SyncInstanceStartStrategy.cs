using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Headers;
using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Http;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.Logging;
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Implements synchronous instance start execution strategy.
/// Executes the workflow instance start directly without background job scheduling.
/// </summary>
public sealed class SyncInstanceStartStrategy(
    IStateMachineExecutor stateMachineExecutor,
    IHeaderService headerService,
    IRuntimeInfoProvider runtimeInfoProvider,
    IInstanceRepository instanceRepository,
    ILogger<SyncInstanceStartStrategy> logger) : IInstanceStartStrategy
{
    /// <inheritdoc />
    public bool CanHandle(bool isSync) => isSync;

    /// <inheritdoc />
    public async Task<InstanceServiceResponse<StartInstanceOutput>> ExecuteAsync(
        InstanceStartExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Executing synchronous start for instance {InstanceId}", context.Instance.Id);

        // Build script context
        var scriptContext = await context.ScriptContextBuilder.BuildAsync(cancellationToken);

        // Execute start transition via StateMachineExecutor
        await stateMachineExecutor.ExecuteTransitionAsync(scriptContext, cancellationToken);
        
        // Add workflow information to response headers
        headerService.AddHeader(
            WorkflowInfo.Name,
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, context.Workflow.Key, context.Workflow.Version, context.Instance.Id)
        );

        var refreshedInstance =  await instanceRepository.FindByIdAsReadOnlyAsync(context.Instance.Id, cancellationToken);
        
        // Build and return response
        return new InstanceServiceResponse<StartInstanceOutput>(new StartInstanceOutput
        {
            Id = refreshedInstance.Id,
            Status = refreshedInstance.Status
        });
    }
}
