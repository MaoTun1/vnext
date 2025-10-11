namespace BBT.Workflow.Execution;

public interface IContextRefresher
{
    Task RefreshAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}