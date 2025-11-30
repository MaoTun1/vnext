using BBT.Aether.Results;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution;

/// <summary>
/// Refreshes the transition execution context with fresh data from the repository.
/// Uses Railway pattern throughout - repository returns Result types.
/// </summary>
public sealed class ContextRefresher(
    IInstanceRepository instanceRepository
) : IContextRefresher
{
    /// <summary>
    /// Refreshes the context by reloading the instance and syncing state.
    /// Railway chain: Reload Instance → Update Context → Sync State
    /// </summary>
    public Task<Result> RefreshAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        return instanceRepository.GetResultAsync(context.InstanceId, true, cancellationToken)
            .Tap(fresh =>
            {
                context.Instance = fresh;
                context.Data = fresh.Data;
            })
            .ThenAsync(_ => SyncStateAsync(context));
    }

    /// <summary>
    /// Synchronizes the current state from the workflow definition.
    /// Uses Tap for side effects (updating context), then converts to non-generic Result.
    /// </summary>
    private Task<Result> SyncStateAsync(TransitionExecutionContext context)
    {
        return Task.FromResult(
            context.Workflow.GetState(context.Instance.GetCurrentState)
                .Tap(state =>
                {
                    context.Current = state;
                    context.Target = null;
                })
                .ToResult());
    }
}