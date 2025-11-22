using BBT.Aether.Results;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution;

public sealed class ContextRefresher(
    IInstanceRepository instanceRepository
) : IContextRefresher
{
    public async Task<Result> RefreshAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        return await ReloadInstanceAsync(context, cancellationToken)
            .ThenAsync(SyncStateAsync);
    }

    private async Task<Result<TransitionExecutionContext>> ReloadInstanceAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(async ct =>
        {
            var fresh = await instanceRepository.GetAsync(context.InstanceId, true, ct);
            context.Instance = fresh;
            context.Data = fresh.Data;
            return context;
        }, cancellationToken);
    }

    private Task<Result> SyncStateAsync(TransitionExecutionContext context)
    {
        var currentStateResult = context.Workflow.GetState(context.Instance.GetCurrentState);
        if (!currentStateResult.IsSuccess)
            return Task.FromResult(Result.Fail(currentStateResult.Error));

        context.Current = currentStateResult.Value!;
        context.Target = null;
        return Task.FromResult(Result.Ok());
    }
}