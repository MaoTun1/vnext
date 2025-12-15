using System.Text.RegularExpressions;

namespace BBT.Workflow.Instances;

/// <summary>
/// Compares InstanceData first by semantic version, then by history sequence for entries with the same version.
/// Supports both simple versions (1.0.0) and extended format (MAJOR.MINOR.PATCH[-PRERELEASE]-pkg.PKG_VERSION+PKG_NAME).
/// </summary>
/// <remarks>
/// Version Format: MAJOR.MINOR.PATCH[-PRERELEASE]-pkg.PKG_VERSION+PKG_NAME[+BUILD_METADATA]
/// <list type="bullet">
///     <item><description>1.0.0 or 1.0.0-alpha.1 → Artifact version (from component JSON file, may include pre-release)</description></item>
///     <item><description>-pkg.1.17.0 → Package version (affects SemVer ordering)</description></item>
///     <item><description>+account → Build metadata (package name, doesn't affect ordering)</description></item>
///     <item><description>+build.123 → Additional build metadata (optional, doesn't affect ordering)</description></item>
/// </list>
/// 
/// Supported Examples:
/// <list type="bullet">
///     <item><description>1.0.0-pkg.1.17.0+account - Standard format</description></item>
///     <item><description>2.1.3-pkg.2.5.1+customer - Different package name</description></item>
///     <item><description>1.0.0-alpha.1-pkg.1.17.0+account - Pre-release artifact version</description></item>
///     <item><description>1.0.0-pkg.1.17.0+account+build.123 - Multiple build metadata</description></item>
/// </list>
/// 
/// Comparison order:
/// <list type="number">
///     <item><description>Compare artifact version (MAJOR.MINOR.PATCH[-PRERELEASE])</description></item>
///     <item><description>If equal, compare package version</description></item>
///     <item><description>Build metadata (+PKG_NAME, +build.xxx) is ignored in comparisons</description></item>
/// </list>
/// </remarks>
public partial class InstanceDataVersionComparer : IComparer<InstanceData>
{
    /// <summary>
    /// Singleton instance of the comparer.
    /// </summary>
    public static InstanceDataVersionComparer Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(InstanceData? x, InstanceData? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        // First compare by version
        var versionComparison = CompareVersionStrings(x.Version, y.Version);

        // If versions are equal, compare by HistorySequence
        if (versionComparison == 0)
        {
            return x.HistorySequence.CompareTo(y.HistorySequence);
        }

        return versionComparison;
    }

    /// <summary>
    /// Compares two version strings.
    /// Supports both simple versions (1.0.0) and extended format (1.0.0-pkg.1.17.0+account).
    /// </summary>
    /// <param name="v1">First version string</param>
    /// <param name="v2">Second version string</param>
    /// <returns>Negative if v1 &lt; v2, zero if equal, positive if v1 &gt; v2</returns>
    internal static int CompareVersionStrings(string? v1, string? v2)
    {
        if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2)) return 0;
        if (string.IsNullOrWhiteSpace(v1)) return -1;
        if (string.IsNullOrWhiteSpace(v2)) return 1;

        var parsed1 = ParseVersion(v1);
        var parsed2 = ParseVersion(v2);

        // First compare artifact versions
        var artifactComparison = CompareSemanticVersion(parsed1.ArtifactVersion, parsed2.ArtifactVersion);
        if (artifactComparison != 0)
        {
            return artifactComparison;
        }

        // If artifact versions are equal, compare package versions
        return CompareSemanticVersion(parsed1.PackageVersion, parsed2.PackageVersion);
    }

    /// <summary>
    /// Parses a version string into its components.
    /// </summary>
    /// <param name="version">Version string (e.g., "1.0.0" or "1.0.0-pkg.1.17.0+account")</param>
    /// <returns>Parsed version components</returns>
    public static ParsedVersion ParseVersion(string version)
    {
        // Remove build metadata (+PKG_NAME) as it doesn't affect comparison
        var buildMetadataIndex = version.IndexOf('+');
        var versionWithoutMetadata = buildMetadataIndex >= 0
            ? version[..buildMetadataIndex]
            : version;

        // Try to match extended format: MAJOR.MINOR.PATCH-pkg.PKG_VERSION
        var match = ExtendedVersionRegex().Match(versionWithoutMetadata);
        if (match.Success)
        {
            return new ParsedVersion(
                match.Groups["artifact"].Value,
                match.Groups["package"].Value
            );
        }

        // Simple format: just artifact version
        return new ParsedVersion(versionWithoutMetadata, null);
    }

    /// <summary>
    /// Compares two semantic version strings.
    /// </summary>
    private static int CompareSemanticVersion(string? v1, string? v2)
    {
        if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2)) return 0;
        if (string.IsNullOrWhiteSpace(v1)) return -1;
        if (string.IsNullOrWhiteSpace(v2)) return 1;

        // Try standard Version parsing first
        if (Version.TryParse(v1, out var version1) && Version.TryParse(v2, out var version2))
        {
            return version1.CompareTo(version2);
        }

        // Fallback to string comparison
        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a version string contains package version information.
    /// </summary>
    /// <param name="version">Version string to check</param>
    /// <returns>True if the version contains "-pkg." indicating package version</returns>
    public static bool HasPackageVersion(string? version)
    {
        return !string.IsNullOrWhiteSpace(version) && version.Contains("-pkg.");
    }

    /// <summary>
    /// Extracts the artifact version from a full version string.
    /// </summary>
    /// <param name="version">Full version string (e.g., "1.0.0-pkg.1.17.0+account")</param>
    /// <returns>Artifact version (e.g., "1.0.0")</returns>
    public static string GetArtifactVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return version;

        var parsed = ParseVersion(version);
        return parsed.ArtifactVersion;
    }

    /// <summary>
    /// Regex pattern for extended version format: MAJOR.MINOR.PATCH[-PRERELEASE]-pkg.PKG_VERSION
    /// Supports pre-release identifiers like -alpha.1, -beta, -rc.1, etc.
    /// </summary>
    /// <remarks>
    /// Pattern breakdown:
    /// - (?&lt;artifact&gt;...) - Captures the artifact version including optional pre-release
    ///   - \d+\.\d+\.\d+ - Base version (e.g., 1.0.0)
    ///   - (?:-[a-zA-Z0-9]+(?:\.[a-zA-Z0-9]+)*)? - Optional pre-release (e.g., -alpha.1, -beta, -rc.1)
    /// - -pkg\. - Literal separator "-pkg."
    /// - (?&lt;package&gt;\d+\.\d+\.\d+) - Package version (e.g., 1.17.0)
    /// </remarks>
    [GeneratedRegex(@"^(?<artifact>\d+\.\d+\.\d+(?:-[a-zA-Z0-9]+(?:\.[a-zA-Z0-9]+)*)?)-pkg\.(?<package>\d+\.\d+\.\d+)$", RegexOptions.Compiled)]
    private static partial Regex ExtendedVersionRegex();

    /// <summary>
    /// Represents a parsed version with artifact and optional package version.
    /// </summary>
    /// <param name="ArtifactVersion">The artifact version (e.g., "1.0.0")</param>
    /// <param name="PackageVersion">The package version (e.g., "1.17.0"), null if not present</param>
    public readonly record struct ParsedVersion(string ArtifactVersion, string? PackageVersion);
}