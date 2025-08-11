namespace BBT.Workflow.Caching;

public interface ICacheSet
{
    Task LoadAllAsync(object data, CancellationToken cancellationToken = default);
}