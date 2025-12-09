using BBT.Workflow.Execution.Strategies;
using BBT.Workflow.Instances;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Aether.Uow;

namespace BBT.Workflow.Execution.Services;

/// <inheritdoc />
public sealed class WorkflowExecutionService(
    IExecutionStrategyFactory execFactory,
    IInstanceRepository instanceRepository) : IWorkflowExecutionService
{
    /// <inheritdoc />
    /// <summary>
    /// Executes a workflow transition using Railway Programming pattern.
    /// Chain reads like a scenario: Get Strategy → Execute → Build Output
    /// All errors are returned as Result without throwing exceptions.
    /// </summary>
    [UnitOfWork]
    [Log]
    [Trace]
    public Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        [Enrich] WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return GetExecutionStrategy(context.Mode)
            .BindAsync(strategy => ExecuteStrategyAsync(strategy, context, cancellationToken))
            .BindAsync(execCtx => BuildTransitionOutputAsync(context, execCtx, cancellationToken));
    }

    /// <summary>
    /// Gets the execution strategy for the specified mode.
    /// </summary>
    private Result<ITransitionStrategy> GetExecutionStrategy(ExecMode mode)
        => execFactory.Get(mode);

    /// <summary>
    /// Executes the strategy with the given context.
    /// Separated for clarity and testability.
    /// </summary>
    private Task<Result<TransitionExecutionContext>> ExecuteStrategyAsync(
        ITransitionStrategy strategy,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
        => strategy.ExecuteAsync(context, cancellationToken);

    /// <summary>
    /// Builds the transition output response.
    /// Uses Railway Programming with Map for type transformation.
    /// </summary>
    private Task<Result<TransitionOutput>> BuildTransitionOutputAsync(
        WorkflowExecutionContext context,
        TransitionExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        // Early return if client response is available (no DB lookup needed)
        if (executionContext.ClientResponse is not null)
        {
            return Task.FromResult(Result<TransitionOutput>.Ok(new TransitionOutput
            {
                Id = executionContext.InstanceId,
                Status = executionContext.ClientResponse.Status
            }));
        }

        // Fetch fresh instance and transform to output using Map
        return FetchInstanceAsync(context.InstanceId, cancellationToken)
            .MapAsync(MapInstanceToOutput);
    }

    /// <summary>
    /// Fetches the workflow instance from repository.
    /// Uses ToResult extension for null-safe Result conversion.
    /// </summary>
    private async Task<Result<Instance>> FetchInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var instance = await instanceRepository.FindByIdentifierAsReadOnlyAsync(instanceId, cancellationToken);
        return instance.ToResult(
            ExecutionErrors.InstanceNotFoundForResponse(instanceId));
    }

    /// <summary>
    /// Maps a workflow instance to TransitionOutput.
    /// Pure transformation function - no side effects.
    /// </summary>
    private static TransitionOutput MapInstanceToOutput(Instance instance)
        => new()
        {
            Id = instance.Id,
            Status = instance.Status
        };
}