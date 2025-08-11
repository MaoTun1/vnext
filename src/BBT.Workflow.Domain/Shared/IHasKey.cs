namespace BBT.Workflow;

public interface IHasKey
{
    /// <summary>
    /// If present, it is the more readable key value of the record.
    /// </summary>
    string Key { get; }
}

public interface IHasDomain
{
    /// <summary>
    /// This is information about the domain on which the stream where the record is located.
    /// </summary>
    public string Domain { get; }
}

public interface IHasVersion
{
    /// <summary>
    /// This is the version information at the time the record is assigned.
    /// </summary>
    public string Version { get; }
}

public interface IHasEtag
{
    string ETag { get; }
}