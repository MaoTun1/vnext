using System.Text.RegularExpressions;

namespace System.Collections.Generic;

public sealed class SemVersionComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;

        var vx = ParseSemVer(x);
        var vy = ParseSemVer(y);

        // 1) Major
        var majorCompare = vx.Major.CompareTo(vy.Major);
        if (majorCompare != 0)
            return majorCompare;

        // 2) Minor
        var minorCompare = vx.Minor.CompareTo(vy.Minor);
        if (minorCompare != 0)
            return minorCompare;

        // 3) Patch
        var patchCompare = vx.Patch.CompareTo(vy.Patch);
        if (patchCompare != 0)
            return patchCompare;

        // 4) PreRelease comparison
        // No PreRelease vs. no (neither) -> considered equal
        // PreRelease is not there vs. is there -> the one that is not there is BIGGER
        if (string.IsNullOrEmpty(vx.PreRelease) && string.IsNullOrEmpty(vy.PreRelease))
            return 0;
        if (string.IsNullOrEmpty(vx.PreRelease))
            return 1; // x is bigger
        if (string.IsNullOrEmpty(vy.PreRelease))
            return -1; // y is bigger

        // Both contain pre-release -> string comparison
        return string.CompareOrdinal(vx.PreRelease, vy.PreRelease);
    }

    /// <summary>
    /// A simple semver parse method
    /// Exp: "1.2.3-alpha.1+build.99"
    /// </summary>
    private SemVersion ParseSemVer(string versionString)
    {
        // 1) Split Build (+)
        var plusIndex = versionString.IndexOf('+');
        var build = plusIndex >= 0 
            ? versionString[(plusIndex + 1)..] // + after is build metadata
            : null;

        var coreAndPreRelease = plusIndex >= 0
            ? versionString[..plusIndex]
            : versionString;

        // 2) Split PreRelease (-)
        var dashIndex = coreAndPreRelease.IndexOf('-');
        var preRelease = dashIndex >= 0
            ? coreAndPreRelease[(dashIndex + 1)..]
            : null;

        var core = dashIndex >= 0
            ? coreAndPreRelease[..dashIndex]
            : coreAndPreRelease;

        // 3) Major, Minor, Patch parse
        var match = Regex.Match(core, @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        var major = 0;
        var minor = 0;
        var patch = 0;

        if (match.Success)
        {
            int.TryParse(match.Groups[1].Value, out major);
            if (match.Groups[2].Success)
                int.TryParse(match.Groups[2].Value, out minor);
            if (match.Groups[3].Success)
                int.TryParse(match.Groups[3].Value, out patch);
        }
        
        return new SemVersion(major, minor, patch, preRelease, build);
    }

    private record SemVersion(int Major, int Minor, int Patch, string? PreRelease, string? Build);
}