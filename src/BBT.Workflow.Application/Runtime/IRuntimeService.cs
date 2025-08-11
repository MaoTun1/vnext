namespace BBT.Workflow.Runtime;

public interface IRuntimeService
{
    Task<IEnumerable<T?>> GetAsync<T>(string schema, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter;

    Task<T?> GetAsync<T>(string schema, string key, string version, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter;
}