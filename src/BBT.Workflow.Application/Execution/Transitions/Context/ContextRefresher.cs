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
        var instanceResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.GetAsync(context.InstanceId, true, ct),
            cancellationToken,
            ex => WorkflowErrors.InstanceNotFound(context.InstanceId, "not found or could not be loaded"));
        
        if (!instanceResult.IsSuccess)
            return Result.Fail(instanceResult.Error);
        
        var fresh = instanceResult.Value!;
        
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