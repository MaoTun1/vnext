using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        JsonData data, bool isLatest, int historySequence = 0) : base(id)
    {
        InstanceId = instanceId;
        SetVersion(version);
        Data = data;
        DataHash = ComputeDataHash(data);
        EnteredAt = DateTime.UtcNow;
        ETag = Ulid.NewUlid().ToString();
        IsLatest = isLatest;
        HistorySequence = historySequence;
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
    /// History sequence number (for ordering history entries within the same version)
    /// </summary>
    public int HistorySequence { get; private set; }

    /// <summary>
    /// IsLatest
    /// </summary>
    public bool? IsLatest { get; private set; }

    /// <summary>
    /// ETag
    /// </summary>
    public string ETag { get; private set; }

    /// <summary>
    /// SHA1 hash of the data payload for change detection
    /// </summary>
    public string DataHash { get; private set; }

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
        Version = Check.NotNullOrWhiteSpace(version, nameof(Version), WorkflowConstants.MaxVersionLength);
    }

    internal InstanceData NewVersion(
        Guid id,
        JsonData jsonData,
        VersionStrategy versionStrategy,
        int historySequence
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
            true,
            historySequence
        );
    }

    /// <summary>
    /// Computes SHA1 hash of the JSON data for change detection
    /// </summary>
    /// <param name="data">The JSON data to hash</param>
    /// <returns>SHA1 hash as hex string</returns>
    private static string ComputeDataHash(JsonData data)
    {
        using var sha1 = SHA1.Create();

        // Use normalized JSON from JsonData for consistent hashing
        var jsonBytes = Encoding.UTF8.GetBytes(data.NormalizedJson);
        var hashBytes = sha1.ComputeHash(jsonBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    internal InstanceData CreateSnapshot()
    {
        var snapshot = new InstanceData
        {
            Id = Id,
            InstanceId = InstanceId,
            Version = Version,
            IsLatest = IsLatest,
            ETag = ETag,
            DataHash = DataHash,
            Data = new JsonData(Data.Json),
            EnteredAt = EnteredAt
        };

        return snapshot;
    }


    /// <summary>
    /// Checks if the provided JSON data has the same content as this instance's data
    /// </summary>
    /// <param name="jsonData">The JSON data to compare</param>
    /// <returns>True if the data is the same, false otherwise</returns>
    public bool HasSameData(JsonData jsonData)
    {
        var otherHash = ComputeDataHash(jsonData);
        return DataHash.Equals(otherHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Marks this instance data as not the latest version
    /// </summary>
    internal void MarkAsNotLatest()
    {
        IsLatest = false;
    }

    private string IncrementVersion(string currentVersion, VersionStrategy versionStrategy)
    {
        var match = Regex.Match(currentVersion, @"^(\d+)\.(\d+)\.(\d+)");
        if (!match.Success)
            return currentVersion;

        int.TryParse(match.Groups[1].Value, out var major);
        int.TryParse(match.Groups[2].Value, out var minor);
        int.TryParse(match.Groups[3].Value, out var patch);

        return versionStrategy.Code switch
        {
            "Major" => $"{major + 1}.0.0",
            "Minor" => $"{major}.{minor + 1}.0",
            "Patch" => $"{major}.{minor}.{patch + 1}",
            _ => currentVersion
        };
    }
}
