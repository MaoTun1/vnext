using System.Text.Json;
using BBT.Aether;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Instance Data
/// </summary>
public sealed class InstanceData : Entity<Guid>, IHasVersion, IHasEtag
{
    private InstanceData()
    {
    }

    internal InstanceData(
        Guid id,
        Guid instanceId,
        string version,
        JsonData data, bool isLatest) : base(id)
    {
        InstanceId = instanceId;
        SetVersion(version);
        Data = data;
        EnteredAt = DateTime.UtcNow;
        ETag = Ulid.NewUlid().ToString();
        IsLatest = isLatest;
    }

    /// <summary>
    /// Instance ID
    /// </summary>
    public Guid InstanceId { get; private set; }

    /// <summary>
    /// Semantic version number. There may be more than one version on the runtime.
    /// </summary>
    public string Version { get; private set; }
    /// <summary>
    /// IsLatest
    /// </summary>
    public bool? IsLatest { get; private set; }

    /// <summary>
    /// ETag
    /// </summary>
    public string ETag { get; private set; }
    
    /// <summary>
    /// <see cref="JsonData"/>
    /// </summary>
    public JsonData Data { get; private set; }

    /// <summary>
    /// Entered at
    /// </summary>
    public DateTime EnteredAt { get; private set; }

    public dynamic? Attributes => Data.JsonElement.ToDynamic();

    private void SetVersion(string version)
    {
        Version = Check.NotNullOrEmpty(version, nameof(Version), WorkflowConstants.MaxVersionLength);
    }

    internal InstanceData NewVersion(
        Guid id,
        JsonData jsonData,
        VersionStrategy versionStrategy
    )
    {
        var newVersion = IncrementVersion(Version, versionStrategy);
        var newData = Data.Merge(jsonData);
        IsLatest = false;
        return new InstanceData(
            id,
            InstanceId,
            newVersion,
            newData,
            true
        );
    }

    private string IncrementVersion(string currentVersion, VersionStrategy versionStrategy)
    {
        var parts = currentVersion.Split('.');
        var major = int.Parse(parts[0]);
        var minor = int.Parse(parts[1]);
        var patch = int.Parse(parts[2]);

        return versionStrategy.Code switch
        {
            "Major" => $"{major + 1}.0.0",
            "Minor" => $"{major}.{minor + 1}.0",
            "Patch" => $"{major}.{minor}.{patch + 1}",
            _ => currentVersion
        };
    }
}