using System;
using System.Linq;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Definitions;

public class WorkflowTimeoutTests
{
    [Fact]
    public void Create_ShouldInitializeProperties()
    {
        // Arrange
        var key = "timeout-key";
        var target = "timeout-state";
        var versionStrategy = "Patch";
        var reset = "none";
        var duration = "PT1H";

        // Act
        var timeout = WorkflowTimeout.Create(key, target, versionStrategy, reset, duration);

        // Assert
        Assert.Equal(key, timeout.Key);
        Assert.Equal(target, timeout.Target);
        Assert.Equal(VersionStrategy.IncreasePatch, timeout.VersionStrategy);
        Assert.NotNull(timeout.Timer);
        Assert.Equal(reset, timeout.Timer.Reset);
        Assert.Equal(duration, timeout.Timer.Duration);
    }

    [Theory]
    [InlineData("timeout-1", "finish", "Minor", "none", "PT30M")]
    [InlineData("timeout-2", "error", "Major", "daily", "PT1H")]
    [InlineData("timeout-3", "cancelled", "Patch", "weekly", "P1D")]
    public void Create_ShouldAcceptValidValues(string key, string target, string versionStrategy, string reset, string duration)
    {
        // Act
        var timeout = WorkflowTimeout.Create(key, target, versionStrategy, reset, duration);

        // Assert
        Assert.Equal(key, timeout.Key);
        Assert.Equal(target, timeout.Target);
        Assert.Equal(reset, timeout.Timer.Reset);
        Assert.Equal(duration, timeout.Timer.Duration);
    }

    [Theory]
    [InlineData(null, "target", "Patch", "none", "PT1H")]
    [InlineData("", "target", "Patch", "none", "PT1H")]
    [InlineData(" ", "target", "Patch", "none", "PT1H")]
    public void Create_ShouldThrowException_WhenKeyIsNullOrEmpty(
        string? key, string target, string versionStrategy, string reset, string duration)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            WorkflowTimeout.Create(key!, target, versionStrategy, reset, duration));
    }

    [Theory]
    [InlineData("key", null, "Patch", "none", "PT1H")]
    [InlineData("key", "", "Patch", "none", "PT1H")]
    [InlineData("key", " ", "Patch", "none", "PT1H")]
    public void Create_ShouldThrowException_WhenTargetIsNullOrEmpty(
        string key, string? target, string versionStrategy, string reset, string duration)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            WorkflowTimeout.Create(key, target!, versionStrategy, reset, duration));
    }

    [Fact]
    public void Create_ShouldThrowException_WhenKeyExceedsMaxLength()
    {
        // Arrange
        var key = new string('a', WorkflowConstants.MaxKeyLength + 1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            WorkflowTimeout.Create(key, "target", "Patch", "none", "PT1H"));
    }

    [Fact]
    public void Create_ShouldThrowException_WhenTargetExceedsMaxLength()
    {
        // Arrange
        var target = new string('a', StateConstants.MaxKeyLength + 1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            WorkflowTimeout.Create("key", target, "Patch", "none", "PT1H"));
    }

    [Fact]
    public void Create_ShouldThrowException_WhenVersionStrategyIsInvalid()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            WorkflowTimeout.Create("key", "target", "Invalid", "none", "PT1H"));
    }

    [Theory]
    [InlineData("Minor")]
    [InlineData("Major")]
    [InlineData("Patch")]
    public void Create_ShouldAcceptValidVersionStrategies(string versionStrategy)
    {
        // Act
        var timeout = WorkflowTimeout.Create("key", "target", versionStrategy, "none", "PT1H");

        // Assert
        Assert.NotNull(timeout.VersionStrategy);
        Assert.Equal(versionStrategy, timeout.VersionStrategy.Code);
    }

    [Fact]
    public void Timer_ShouldBeInitialized()
    {
        // Act
        var timeout = WorkflowTimeout.Create("key", "target", "Patch", "none", "PT1H");

        // Assert
        Assert.NotNull(timeout.Timer);
        Assert.IsType<TimerConfig>(timeout.Timer);
    }

    [Fact]
    public void Create_ShouldAcceptMaxLengthValues()
    {
        // Arrange
        var key = new string('a', WorkflowConstants.MaxKeyLength);
        var target = new string('b', StateConstants.MaxKeyLength);
        var reset = new string('c', WorkflowConstants.MaxTimerResetLength);
        var duration = new string('d', WorkflowConstants.MaxDurationLength);

        // Act
        var timeout = WorkflowTimeout.Create(key, target, "Patch", reset, duration);

        // Assert
        Assert.Equal(key, timeout.Key);
        Assert.Equal(target, timeout.Target);
        Assert.Equal(reset, timeout.Timer.Reset);
        Assert.Equal(duration, timeout.Timer.Duration);
    }

    [Fact]
    public void VersionStrategy_ShouldBeMappedCorrectly_ForMinor()
    {
        // Act
        var timeout = WorkflowTimeout.Create("key", "target", "Minor", "none", "PT1H");

        // Assert
        Assert.Equal(VersionStrategy.IncreaseMinor, timeout.VersionStrategy);
    }

    [Fact]
    public void VersionStrategy_ShouldBeMappedCorrectly_ForMajor()
    {
        // Act
        var timeout = WorkflowTimeout.Create("key", "target", "Major", "none", "PT1H");

        // Assert
        Assert.Equal(VersionStrategy.IncreaseMajor, timeout.VersionStrategy);
    }

    [Fact]
    public void VersionStrategy_ShouldBeMappedCorrectly_ForPatch()
    {
        // Act
        var timeout = WorkflowTimeout.Create("key", "target", "Patch", "none", "PT1H");

        // Assert
        Assert.Equal(VersionStrategy.IncreasePatch, timeout.VersionStrategy);
    }

    [Fact]
    public void Key_ShouldImplementIHasKey()
    {
        // Act
        var timeout = WorkflowTimeout.Create("key", "target", "Patch", "none", "PT1H");

        // Assert
        Assert.IsAssignableFrom<IHasKey>(timeout);
        Assert.Equal("key", ((IHasKey)timeout).Key);
    }
}

