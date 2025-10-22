using System;
using System.Linq;
using Xunit;

namespace BBT.Workflow;

public class VersionStrategyTests
{
    [Fact]
    public void FromCode_ShouldReturnIncreaseMinor_WhenCodeIsMinor()
    {
        // Arrange & Act
        var result = VersionStrategy.FromCode("Minor");

        // Assert
        Assert.Equal(VersionStrategy.IncreaseMinor, result);
        Assert.Equal("Minor", result.Code);
        Assert.Equal("Increase Minor", result.Description);
    }

    [Fact]
    public void FromCode_ShouldReturnIncreaseMajor_WhenCodeIsMajor()
    {
        // Arrange & Act
        var result = VersionStrategy.FromCode("Major");

        // Assert
        Assert.Equal(VersionStrategy.IncreaseMajor, result);
        Assert.Equal("Major", result.Code);
        Assert.Equal("Increase Minor", result.Description);
    }

    [Fact]
    public void FromCode_ShouldReturnIncreasePatch_WhenCodeIsPatch()
    {
        // Arrange & Act
        var result = VersionStrategy.FromCode("Patch");

        // Assert
        Assert.Equal(VersionStrategy.IncreasePatch, result);
        Assert.Equal("Patch", result.Code);
        Assert.Equal("Increase Patch", result.Description);
    }

    [Fact]
    public void FromCode_ShouldThrowArgumentException_WhenCodeIsInvalid()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => VersionStrategy.FromCode("Invalid"));
        Assert.Contains("Unknown status code: Invalid", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("PATCH")]
    [InlineData("minor")]
    [InlineData("MAJOR")]
    public void FromCode_ShouldThrowArgumentException_WhenCodeIsNotExactMatch(string code)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => VersionStrategy.FromCode(code));
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingSameStrategy()
    {
        // Arrange
        var strategy1 = VersionStrategy.IncreaseMinor;
        var strategy2 = VersionStrategy.FromCode("Minor");

        // Act & Assert
        Assert.True(strategy1.Equals(strategy2));
        Assert.Equal(strategy1, strategy2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingDifferentStrategies()
    {
        // Arrange
        var strategy1 = VersionStrategy.IncreaseMinor;
        var strategy2 = VersionStrategy.IncreaseMajor;

        // Act & Assert
        Assert.False(strategy1.Equals(strategy2));
        Assert.NotEqual(strategy1, strategy2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingWithNull()
    {
        // Arrange
        var strategy = VersionStrategy.IncreaseMinor;

        // Act & Assert
        Assert.False(strategy.Equals(null));
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForEqualStrategies()
    {
        // Arrange
        var strategy1 = VersionStrategy.IncreaseMinor;
        var strategy2 = VersionStrategy.FromCode("Minor");

        // Act & Assert
        Assert.Equal(strategy1.GetHashCode(), strategy2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferent_ForDifferentStrategies()
    {
        // Arrange
        var strategy1 = VersionStrategy.IncreaseMinor;
        var strategy2 = VersionStrategy.IncreasePatch;

        // Act & Assert
        Assert.NotEqual(strategy1.GetHashCode(), strategy2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnCorrectFormat_ForMinor()
    {
        // Arrange
        var strategy = VersionStrategy.IncreaseMinor;

        // Act
        var result = strategy.ToString();

        // Assert
        Assert.Equal("Increase Minor (Minor)", result);
    }

    [Fact]
    public void ToString_ShouldReturnCorrectFormat_ForMajor()
    {
        // Arrange
        var strategy = VersionStrategy.IncreaseMajor;

        // Act
        var result = strategy.ToString();

        // Assert
        Assert.Equal("Increase Minor (Major)", result);
    }

    [Fact]
    public void ToString_ShouldReturnCorrectFormat_ForPatch()
    {
        // Arrange
        var strategy = VersionStrategy.IncreasePatch;

        // Act
        var result = strategy.ToString();

        // Assert
        Assert.Equal("Increase Patch (Patch)", result);
    }

    [Theory]
    [InlineData("Minor", "Increase Minor (Minor)")]
    [InlineData("Major", "Increase Minor (Major)")]
    [InlineData("Patch", "Increase Patch (Patch)")]
    public void ToString_ShouldReturnCorrectFormat_ForAllStrategies(string code, string expected)
    {
        // Arrange
        var strategy = VersionStrategy.FromCode(code);

        // Act
        var result = strategy.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StaticInstances_ShouldBeSingleton()
    {
        // Arrange & Act
        var minor1 = VersionStrategy.IncreaseMinor;
        var minor2 = VersionStrategy.IncreaseMinor;

        // Assert
        Assert.Same(minor1, minor2);
    }

    [Fact]
    public void AllStaticInstances_ShouldBeAvailable()
    {
        // Assert
        Assert.NotNull(VersionStrategy.IncreaseMinor);
        Assert.NotNull(VersionStrategy.IncreaseMajor);
        Assert.NotNull(VersionStrategy.IncreasePatch);
    }

    [Fact]
    public void Code_ShouldNotBeNull()
    {
        // Arrange
        var strategies = new[]
        {
            VersionStrategy.IncreaseMinor,
            VersionStrategy.IncreaseMajor,
            VersionStrategy.IncreasePatch
        };

        // Act & Assert
        foreach (var strategy in strategies)
        {
            Assert.NotNull(strategy.Code);
            Assert.NotEmpty(strategy.Code);
        }
    }

    [Fact]
    public void Description_ShouldNotBeNull()
    {
        // Arrange
        var strategies = new[]
        {
            VersionStrategy.IncreaseMinor,
            VersionStrategy.IncreaseMajor,
            VersionStrategy.IncreasePatch
        };

        // Act & Assert
        foreach (var strategy in strategies)
        {
            Assert.NotNull(strategy.Description);
            Assert.NotEmpty(strategy.Description);
        }
    }
}

