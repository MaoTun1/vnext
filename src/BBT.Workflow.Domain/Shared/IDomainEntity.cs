namespace BBT.Workflow;

public interface IDomainEntity : IHasKey, IHasVersion, IHasDomain
{
    string ComponentKey { get; }
}