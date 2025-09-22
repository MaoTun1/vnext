namespace BBT.Workflow.Caching;

public interface ICacheSet : IDisposable
{
    Task LoadAllAsync(object data, CancellationToken cancellationToken = default);
}