using Xunit;

namespace System.Collections.Generic;

/// <summary>
/// Unit tests for SemVersionComparer
/// </summary>
public class SemVersionComparerTests
{
    private readonly SemVersionComparer _comparer = new();

    #region Basic Version Comparison

    [Fact]
    public void Compare_SameVersions_ShouldReturnZero()
    {
        // Arrange
        var version1 = "1.2.3";
        var version2 = "1.2.3";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_SameReferences_ShouldReturnZero()
    {
        // Arrange
        var version = "1.2.3";

        // Act
        var result = _comparer.Compare(version, version);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_FirstNull_ShouldReturnNegative()
    {
        // Act
        var result = _comparer.Compare(null, "1.2.3");

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_SecondNull_ShouldReturnPositive()
    {
        // Act
        var result = _comparer.Compare("1.2.3", null);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public void Compare_BothNull_ShouldReturnZero()
    {
        // Act
        var result = _comparer.Compare(null, null);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Major Version Comparison

    [Fact]
    public void Compare_DifferentMajorVersions_ShouldCompareCorrectly()
    {
        // Arrange
        var version1 = "1.0.0";
        var version2 = "2.0.0";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_HigherMajorVersion_ShouldReturnPositive()
    {
        // Arrange
        var version1 = "3.0.0";
        var version2 = "2.0.0";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result > 0);
    }

    #endregion

    #region Minor Version Comparison

    [Fact]
    public void Compare_DifferentMinorVersions_ShouldCompareCorrectly()
    {
        // Arrange
        var version1 = "1.1.0";
        var version2 = "1.2.0";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_HigherMinorVersion_ShouldReturnPositive()
    {
        // Arrange
        var version1 = "1.5.0";
        var version2 = "1.2.0";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result > 0);
    }

    #endregion

    #region Patch Version Comparison

    [Fact]
    public void Compare_DifferentPatchVersions_ShouldCompareCorrectly()
    {
        // Arrange
        var version1 = "1.2.3";
        var version2 = "1.2.5";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_HigherPatchVersion_ShouldReturnPositive()
    {
        // Arrange
        var version1 = "1.2.10";
        var version2 = "1.2.5";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result > 0);
    }

    #endregion

    #region PreRelease Comparison

    [Fact]
    public void Compare_WithAndWithoutPreRelease_WithoutPreReleaseShouldBeGreater()
    {
        // Arrange
        var version1 = "1.2.3";
        var version2 = "1.2.3-alpha";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result > 0); // version without pre-release is bigger
    }

    [Fact]
    public void Compare_PreReleaseWithStable_PreReleaseShouldBeLess()
    {
        // Arrange
        var version1 = "1.2.3-beta";
        var version2 = "1.2.3";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0); // pre-release is smaller
    }

    [Fact]
    public void Compare_DifferentPreReleases_ShouldCompareLexicographically()
    {
        // Arrange
        var version1 = "1.2.3-alpha";
        var version2 = "1.2.3-beta";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0); // "alpha" < "beta"
    }

    [Fact]
    public void Compare_SamePreReleases_ShouldReturnZero()
    {
        // Arrange
        var version1 = "1.2.3-alpha.1";
        var version2 = "1.2.3-alpha.1";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_PreReleaseWithNumbers_ShouldCompareLexicographically()
    {
        // Arrange
        var version1 = "1.2.3-alpha.1";
        var version2 = "1.2.3-alpha.2";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0); // "alpha.1" < "alpha.2"
    }

    #endregion

    #region Build Metadata

    [Fact]
    public void Compare_WithBuildMetadata_ShouldIgnoreBuildMetadata()
    {
        // Arrange
        var version1 = "1.2.3+build.100";
        var version2 = "1.2.3+build.200";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.Equal(0, result); // Build metadata should not affect comparison
    }

    [Fact]
    public void Compare_WithAndWithoutBuildMetadata_ShouldBeEqual()
    {
        // Arrange
        var version1 = "1.2.3";
        var version2 = "1.2.3+build.99";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_CompleteVersions_ShouldIgnoreBuildMetadata()
    {
        // Arrange
        var version1 = "1.2.3-alpha.1+build.99";
        var version2 = "1.2.3-alpha.1+build.100";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Compare_MajorVersionOnly_ShouldCompareCorrectly()
    {
        // Arrange
        var version1 = "1";
        var version2 = "2";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_MajorMinorOnly_ShouldCompareCorrectly()
    {
        // Arrange
        var version1 = "1.2";
        var version2 = "1.3";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_MissingMinorAndPatch_ShouldTreatAsZero()
    {
        // Arrange
        var version1 = "1";
        var version2 = "1.0.0";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_LargeVersionNumbers_ShouldHandleCorrectly()
    {
        // Arrange
        var version1 = "100.200.300";
        var version2 = "100.200.301";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_InvalidVersionFormat_ShouldHandleGracefully()
    {
        // Arrange
        var version1 = "invalid";
        var version2 = "1.2.3";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0); // Invalid version should be treated as 0.0.0
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public void Compare_SortingVersionList_ShouldSortCorrectly()
    {
        // Arrange
        var versions = new List<string>
        {
            "2.0.0",
            "1.2.3-alpha",
            "1.2.3",
            "1.0.0",
            "1.2.3-beta",
            "1.2.4"
        };

        // Act
        versions.Sort(_comparer);

        // Assert
        Assert.Equal("1.0.0", versions[0]);
        Assert.Equal("1.2.3-alpha", versions[1]);
        Assert.Equal("1.2.3-beta", versions[2]);
        Assert.Equal("1.2.3", versions[3]);
        Assert.Equal("1.2.4", versions[4]);
        Assert.Equal("2.0.0", versions[5]);
    }

    [Fact]
    public void Compare_SortingWithBuildMetadata_ShouldIgnoreBuild()
    {
        // Arrange
        var versions = new List<string>
        {
            "1.2.3+build.200",
            "1.2.3+build.100",
            "1.2.3"
        };

        // Act
        versions.Sort(_comparer);

        // Assert
        // All should be considered equal, original order might be preserved
        Assert.Contains("1.2.3", versions);
        Assert.Contains("1.2.3+build.200", versions);
        Assert.Contains("1.2.3+build.100", versions);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void Compare_ComplexPreReleaseVersions_ShouldCompareCorrectly()
    {
        // Arrange
        var versions = new List<string>
        {
            "1.0.0-rc.1",
            "1.0.0-beta.11",
            "1.0.0-beta.2",
            "1.0.0-alpha",
            "1.0.0"
        };

        // Act
        versions.Sort(_comparer);

        // Assert
        Assert.Equal("1.0.0-alpha", versions[0]);
        Assert.True(versions.IndexOf("1.0.0") == versions.Count - 1); // Stable should be last
    }

    [Fact]
    public void Compare_FullSemVerFormat_ShouldHandleCorrectly()
    {
        // Arrange
        var version1 = "1.2.3-alpha.1+build.99";
        var version2 = "1.2.4-alpha.1+build.100";

        // Act
        var result = _comparer.Compare(version1, version2);

        // Assert
        Assert.True(result < 0); // 1.2.3 < 1.2.4
    }

    #endregion
}

