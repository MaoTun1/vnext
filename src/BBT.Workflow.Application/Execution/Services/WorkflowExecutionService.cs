using BBT.Workflow.Execution.Strategies;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using BBT.Aether.Aspects;
using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Services;

/// <inheritdoc />
public sealed class WorkflowExecutionService(
    IExecutionStrategyFactory execFactory,
    ICurrentSchema currentSchema,
    IInstanceRepository instanceRepository) : IWorkflowExecutionService
{
    /// <inheritdoc />
    /// <summary>
    /// Executes a workflow transition using Railway Programming pattern.
    /// All errors are returned as Result without throwing exceptions.
    /// </summary>
    [UnitOfWork]
    [Log]
    [Trace]
    public async Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        [Enrich] WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        using (currentSchema.Change(context.WorkflowKey))
        {
            // Railway Programming: Get strategy → Execute → Build output
            var strategyResult = await GetExecutionStrategyAsync(context.Mode);
            if (!strategyResult.IsSuccess)
                return Result<TransitionOutput>.Fail(strategyResult.Error);

            var executionResult = await strategyResult.Value!.ExecuteAsync(context, cancellationToken);
            
            if (!executionResult.IsSuccess)
                return Result<TransitionOutput>.Fail(executionResult.Error);

            return await BuildTransitionOutputAsync(context, executionResult.Value!, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the execution strategy for the specified mode.
    /// Returns Result to avoid throwing exceptions.
    /// </summary>
    private Task<Result<ITransitionStrategy>> GetExecutionStrategyAsync(ExecMode mode)
    {
        return Task.FromResult(execFactory.Get(mode));
    }

    /// <summary>
    /// Builds the transition output response.
    /// Uses Railway Programming to handle potential null instance scenario.
    /// </summary>
    private async Task<Result<TransitionOutput>> BuildTransitionOutputAsync(
        WorkflowExecutionContext context,
        TransitionExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        // If client response is available, use it directly
        if (executionContext.ClientResponse is not null)
        {
            return Result<TransitionOutput>.Ok(new TransitionOutput
            {
                Id = executionContext.InstanceId,
                Status = executionContext.ClientResponse.Status
            });
        }

        // Otherwise, fetch fresh instance and build response
        var instanceResult = await FetchInstanceAsync(context.InstanceId, cancellationToken);
        
        if (!instanceResult.IsSuccess)
            return Result<TransitionOutput>.Fail(instanceResult.Error);

        return await BuildOutputFromInstanceAsync(instanceResult.Value!);
    }

    /// <summary>
    /// Fetches the workflow instance from repository.
    /// Returns Result with NotFound error if instance doesn't exist.
    /// </summary>
    private async Task<Result<Instance>> FetchInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instance = await instanceRepository.FindByIdAsReadOnlyAsync(instanceId, cancellationToken);
        
        if (instance is null)
        {
            return Result<Instance>.Fail(
                Error.NotFound(
                    WorkflowErrorCodes.NotFoundInstanceData,
                    $"Workflow instance {instanceId} not found during transition response build.",
                    instanceId.ToString()));
        }

        return Result<Instance>.Ok(instance);
    }

    /// <summary>
    /// Builds TransitionOutput from a workflow instance.
    /// </summary>
    private Task<Result<TransitionOutput>> BuildOutputFromInstanceAsync(Instance instance)
    {
        var output = new TransitionOutput
        {
            Id = instance.Id,
            Status = instance.Status
        };

        return Task.FromResult(Result<TransitionOutput>.Ok(output));
    }
}