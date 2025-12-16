using BBT.Workflow.Instances;

namespace System.Collections.Generic;

/// <summary>
/// Compares semantic version strings with support for extended version format.
/// Delegates to <see cref="InstanceDataVersionComparer.CompareVersionStrings"/> for consistent comparison logic.
/// </summary>
/// <remarks>
/// Supports both simple versions (1.0.0) and extended format (MAJOR.MINOR.PATCH[-PRERELEASE]-pkg.PKG_VERSION+PKG_NAME).
/// <list type="bullet">
///     <item><description>1.0.0-pkg.1.17.0+account - Standard format</description></item>
///     <item><description>2.1.3-pkg.2.5.1+customer - Different package name</description></item>
///     <item><description>1.0.0-alpha.1-pkg.1.17.0+account - Pre-release artifact version</description></item>
///     <item><description>1.0.0-pkg.1.17.0+account+build.123 - Multiple build metadata</description></item>
/// </list>
/// </remarks>
public sealed class SemVersionComparer : IComparer<string>
{
    /// <summary>
    /// Compares two version strings using the extended version comparison logic.
    /// </summary>
    /// <param name="x">First version string</param>
    /// <param name="y">Second version string</param>
    /// <returns>Negative if x &lt; y, zero if equal, positive if x &gt; y</returns>
    public int Compare(string? x, string? y)
    {
        // Delegate to InstanceDataVersionComparer for consistent comparison across the application
        return InstanceDataVersionComparer.CompareVersionStrings(x, y);
    }
}