using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution;

public sealed class ContextRefresher(
    IInstanceRepository instanceRepository
    ): IContextRefresher
{
    public async Task RefreshAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        var fresh = await instanceRepository.GetAsync(context.InstanceId, true, cancellationToken);
        context.Instance = fresh;
        context.ConcurrencyToken = fresh.ConcurrencyStamp;
        context.Data = fresh.Data;
        context.Current = context.Workflow.GetState(fresh.GetCurrentState);
        context.Target = null;
    }
}