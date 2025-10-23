using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BBT.Workflow.Instances;

/// <summary>
/// Unit tests for InstanceDataVersionComparer
/// </summary>
public class InstanceDataVersionComparerTests : DomainTestBase<DomainEntryPoint>
{
    private readonly InstanceDataVersionComparer _comparer;

    public InstanceDataVersionComparerTests()
    {
        _comparer = InstanceDataVersionComparer.Instance;
    }

    [Fact]
    public void Instance_ShouldReturnSingletonInstance()
    {
        // Arrange & Act
        var instance1 = InstanceDataVersionComparer.Instance;
        var instance2 = InstanceDataVersionComparer.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)] // 1.0.0 < 1.0.1
    [InlineData("1.0.1", "1.0.0", 1)]  // 1.0.1 > 1.0.0
    [InlineData("1.0.0", "1.0.0", 0)]  // 1.0.0 == 1.0.0
    [InlineData("1.0.0", "1.1.0", -1)] // 1.0.0 < 1.1.0
    [InlineData("1.1.0", "1.0.0", 1)]  // 1.1.0 > 1.0.0
    [InlineData("1.0.0", "2.0.0", -1)] // 1.0.0 < 2.0.0
    [InlineData("2.0.0", "1.0.0", 1)]  // 2.0.0 > 1.0.0
    [InlineData("1.2.3", "1.2.4", -1)] // 1.2.3 < 1.2.4
    [InlineData("1.2.4", "1.2.3", 1)]  // 1.2.4 > 1.2.3
    public void Compare_ShouldReturnCorrectResult_ForSemanticVersions(string version1, string version2, int expected)
    {
        // Arrange
        var data1 = CreateInstanceData(version1);
        var data2 = CreateInstanceData(version2);

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        if (expected < 0)
            Assert.True(result < 0, $"Expected {version1} < {version2}");
        else if (expected > 0)
            Assert.True(result > 0, $"Expected {version1} > {version2}");
        else
            Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_ShouldReturnZero_WhenBothAreNull()
    {
        // Arrange
        InstanceData? data1 = null;
        InstanceData? data2 = null;

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_ShouldReturnNegative_WhenFirstIsNull()
    {
        // Arrange
        InstanceData? data1 = null;
        var data2 = CreateInstanceData("1.0.0");

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_ShouldReturnPositive_WhenSecondIsNull()
    {
        // Arrange
        var data1 = CreateInstanceData("1.0.0");
        InstanceData? data2 = null;

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        Assert.True(result > 0);
    }

    [Theory]
    [InlineData(null, null, 0)]
    [InlineData(null, "1.0.0", -1)]
    [InlineData("1.0.0", null, 1)]
    public void Compare_ShouldHandleNullVersions(string? version1, string? version2, int expected)
    {
        // Arrange
        // InstanceData boş string version kabul etmez, sadece null test ediyoruz
        var data1 = version1 != null ? CreateInstanceData(version1) : null;
        var data2 = version2 != null ? CreateInstanceData(version2) : null;

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        if (expected < 0)
            Assert.True(result < 0);
        else if (expected > 0)
            Assert.True(result > 0);
        else
            Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("invalid1", "invalid2", -1)] // String comparison fallback: "invalid1" < "invalid2"
    [InlineData("abc", "xyz", -1)]           // String comparison fallback
    [InlineData("xyz", "abc", 1)]            // String comparison fallback
    public void Compare_ShouldFallbackToStringComparison_ForInvalidVersions(string version1, string version2, int expected)
    {
        // Arrange
        var data1 = CreateInstanceData(version1);
        var data2 = CreateInstanceData(version2);

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        if (expected < 0)
            Assert.True(result < 0);
        else if (expected > 0)
            Assert.True(result > 0);
        else
            Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0.0")]
    [InlineData("1.0", "1.0.0")]
    public void Compare_ShouldHandleDifferentVersionFormats(string version1, string version2)
    {
        // Arrange
        var data1 = CreateInstanceData(version1);
        var data2 = CreateInstanceData(version2);

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        // Result depends on Version.TryParse behavior
        // This test ensures no exceptions are thrown
        Assert.NotNull(result);
    }

    [Fact]
    public void Compare_WithList_ShouldSortCorrectly()
    {
        // Arrange
        var list = new List<InstanceData>
        {
            CreateInstanceData("2.0.0"),
            CreateInstanceData("1.0.0"),
            CreateInstanceData("1.5.0"),
            CreateInstanceData("1.0.1"),
            CreateInstanceData("3.0.0")
        };

        // Act
        list.Sort(_comparer);

        // Assert
        Assert.Equal("1.0.0", list[0].Version);
        Assert.Equal("1.0.1", list[1].Version);
        Assert.Equal("1.5.0", list[2].Version);
        Assert.Equal("2.0.0", list[3].Version);
        Assert.Equal("3.0.0", list[4].Version);
    }

    [Fact]
    public void Compare_WithOrderBy_ShouldSortCorrectly()
    {
        // Arrange
        var list = new List<InstanceData>
        {
            CreateInstanceData("2.1.0"),
            CreateInstanceData("1.0.0"),
            CreateInstanceData("2.0.0"),
            CreateInstanceData("1.5.0")
        };

        // Act
        var sorted = list.OrderBy(x => x, _comparer).ToList();

        // Assert
        Assert.Equal("1.0.0", sorted[0].Version);
        Assert.Equal("1.5.0", sorted[1].Version);
        Assert.Equal("2.0.0", sorted[2].Version);
        Assert.Equal("2.1.0", sorted[3].Version);
    }

    [Fact]
    public void Compare_WithOrderByDescending_ShouldSortCorrectly()
    {
        // Arrange
        var list = new List<InstanceData>
        {
            CreateInstanceData("1.0.0"),
            CreateInstanceData("2.1.0"),
            CreateInstanceData("1.5.0"),
            CreateInstanceData("2.0.0")
        };

        // Act
        var sorted = list.OrderByDescending(x => x, _comparer).ToList();

        // Assert
        Assert.Equal("2.1.0", sorted[0].Version);
        Assert.Equal("2.0.0", sorted[1].Version);
        Assert.Equal("1.5.0", sorted[2].Version);
        Assert.Equal("1.0.0", sorted[3].Version);
    }

    [Fact]
    public void Compare_ShouldBeConsistent()
    {
        // Arrange
        var data1 = CreateInstanceData("1.0.0");
        var data2 = CreateInstanceData("2.0.0");

        // Act
        var result1 = _comparer.Compare(data1, data2);
        var result2 = _comparer.Compare(data2, data1);

        // Assert
        Assert.True(result1 < 0);
        Assert.True(result2 > 0);
        Assert.Equal(-Math.Sign(result1), Math.Sign(result2));
    }

    [Theory]
    [InlineData("10.0.0", "2.0.0", 1)]  // 10 > 2 numerically
    [InlineData("1.10.0", "1.2.0", 1)]  // 10 > 2 numerically
    [InlineData("1.0.10", "1.0.2", 1)]  // 10 > 2 numerically
    public void Compare_ShouldUseNumericComparison_NotLexicographic(string version1, string version2, int expected)
    {
        // Arrange
        var data1 = CreateInstanceData(version1);
        var data2 = CreateInstanceData(version2);

        // Act
        var result = _comparer.Compare(data1, data2);

        // Assert
        if (expected < 0)
            Assert.True(result < 0);
        else if (expected > 0)
            Assert.True(result > 0);
        else
            Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_WithMixedValidAndInvalidVersions_ShouldNotThrow()
    {
        // Arrange
        var instance = InstanceFactory.CreateDefault();
        var list = new List<InstanceData>
        {
            instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{}"), "1.0.0"),
            instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{}"), "invalid"),
            instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{}"), "2.0.0"),
            instance.AddDataWithVersion(Guid.NewGuid(), JsonData.CreateFrom("{}"), "another-invalid")
        };

        // Act & Assert - Should not throw
        var sorted = list.OrderBy(x => x, _comparer).ToList();
        Assert.Equal(4, sorted.Count);
    }

    private InstanceData CreateInstanceData(string version)
    {
        var instance = InstanceFactory.CreateDefault();
        return instance.AddDataWithVersion(
            Guid.NewGuid(),
            JsonData.CreateFrom("{}"),
            version
        );
    }
}

