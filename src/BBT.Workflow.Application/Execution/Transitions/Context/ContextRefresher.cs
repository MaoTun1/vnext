using BBT.Workflow.Domain;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution;

public sealed class ContextRefresher(
    IInstanceRepository instanceRepository
    ): IContextRefresher
{
    public async Task<Result> RefreshAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // 1. Get fresh instance
        var fresh = await instanceRepository.GetAsync(context.InstanceId, true, cancellationToken);
        
        // 2. Update instance data
        context.Instance = fresh;
        context.ConcurrencyToken = fresh.ConcurrencyStamp;
        context.Data = fresh.Data;
        
        // 3. Get current state using Result Pattern
        var currentStateResult = context.Workflow.GetState(fresh.GetCurrentState);
        if (!currentStateResult.IsSuccess)
            return Result.Fail(currentStateResult.Error);
        
        // 4. Update state
        context.Current = currentStateResult.Value!;
        context.Target = null;
        
        return Result.Ok();
    }
}