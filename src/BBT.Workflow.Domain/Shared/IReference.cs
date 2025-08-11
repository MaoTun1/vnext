namespace BBT.Workflow;

public interface IReference : IHasKey, IHasVersion, IHasDomain
{
    /// <summary>
    /// It is the information on which stream the record is located.
    /// </summary>
    string Flow { get; }
}

public interface IReferenceSetter
{
    void SetReference(IReference reference);
}

public sealed class Reference(
    string key,
    string domain,
    string flow,
    string version)
    : IReference
{
    /// <summary>
    /// If present, it is the more readable key value of the record.
    /// </summary>
    public string Key { get; private set; } = key;

    /// <summary>
    /// It is the information on which stream the record is located.
    /// </summary>
    public string Flow { get; private set; } = flow;

    /// <summary>
    /// Information about which domain the flow is working on and which domain it belongs to.
    /// </summary>
    public string Domain { get; private set; } = domain;

    /// <summary>
    /// This is the version information at the time the record is assigned.
    /// </summary>
    public string Version { get; private set; } = version;
}

public static class ReferenceExtensions
{
    public static Reference ToReference(this IReference reference)
    {
        return new Reference(
            reference.Key,
            reference.Domain,
            reference.Flow,
            reference.Version);
    }
}