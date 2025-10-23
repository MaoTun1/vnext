using System;
using System.Collections.Generic;
using System.Linq;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Definitions;

public class TimerConfigTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var reset = "none";
        var duration = "PT1H";

        // Act
        var timerConfig = new TimerConfig(reset, duration);

        // Assert
        Assert.Equal(reset, timerConfig.Reset);
        Assert.Equal(duration, timerConfig.Duration);
    }

    [Theory]
    [InlineData("none", "PT1H")]
    [InlineData("daily", "PT30M")]
    [InlineData("weekly", "P1D")]
    [InlineData("monthly", "PT2H30M")]
    public void Constructor_ShouldAcceptValidValues(string reset, string duration)
    {
        // Act
        var timerConfig = new TimerConfig(reset, duration);

        // Assert
        Assert.Equal(reset, timerConfig.Reset);
        Assert.Equal(duration, timerConfig.Duration);
    }

    [Theory]
    [InlineData(null, "PT1H")]
    [InlineData("", "PT1H")]
    [InlineData(" ", "PT1H")]
    public void Constructor_ShouldThrowException_WhenResetIsNullOrEmpty(string? reset, string duration)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimerConfig(reset!, duration));
    }

    [Theory]
    [InlineData("none", null)]
    [InlineData("none", "")]
    [InlineData("none", " ")]
    public void Constructor_ShouldThrowException_WhenDurationIsNullOrEmpty(string reset, string? duration)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimerConfig(reset, duration!));
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenResetExceedsMaxLength()
    {
        // Arrange
        var reset = new string('a', WorkflowConstants.MaxTimerResetLength + 1);
        var duration = "PT1H";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimerConfig(reset, duration));
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenDurationExceedsMaxLength()
    {
        // Arrange
        var reset = "none";
        var duration = new string('a', WorkflowConstants.MaxDurationLength + 1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TimerConfig(reset, duration));
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenPropertiesAreSame()
    {
        // Arrange
        var reset = "none";
        var duration = "PT1H";
        var timerConfig1 = new TimerConfig(reset, duration);
        var timerConfig2 = new TimerConfig(reset, duration);

        // Act & Assert
        Assert.True(timerConfig1.ValueEquals(timerConfig2));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenResetDiffers()
    {
        // Arrange
        var timerConfig1 = new TimerConfig("none", "PT1H");
        var timerConfig2 = new TimerConfig("daily", "PT1H");

        // Act & Assert
        Assert.NotEqual(timerConfig1, timerConfig2);
        Assert.False(timerConfig1.Equals(timerConfig2));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenDurationDiffers()
    {
        // Arrange
        var timerConfig1 = new TimerConfig("none", "PT1H");
        var timerConfig2 = new TimerConfig("none", "PT2H");

        // Act & Assert
        Assert.NotEqual(timerConfig1, timerConfig2);
        Assert.False(timerConfig1.Equals(timerConfig2));
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferent_ForDifferentObjects()
    {
        // Arrange
        var timerConfig1 = new TimerConfig("none", "PT1H");
        var timerConfig2 = new TimerConfig("daily", "PT2H");

        // Act & Assert
        Assert.NotEqual(timerConfig1.GetHashCode(), timerConfig2.GetHashCode());
    }

    [Fact]
    public void GetAtomicValues_ShouldReturnResetAndDuration()
    {
        // Arrange
        var reset = "none";
        var duration = "PT1H";
        var timerConfig = new TimerConfig(reset, duration);

        // Act
        var atomicValues = timerConfig.GetType()
            .GetMethod("GetAtomicValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(timerConfig, null) as IEnumerable<object>;

        // Assert
        Assert.NotNull(atomicValues);
        var valuesList = atomicValues.ToList();
        Assert.Equal(2, valuesList.Count);
        Assert.Equal(reset, valuesList[0]);
        Assert.Equal(duration, valuesList[1]);
    }

    [Theory]
    [InlineData("PT1H")]
    [InlineData("PT30M")]
    [InlineData("P1D")]
    [InlineData("PT2H30M")]
    public void Duration_ShouldAcceptValidISO8601Formats(string duration)
    {
        // Arrange & Act
        var timerConfig = new TimerConfig("none", duration);

        // Assert
        Assert.Equal(duration, timerConfig.Duration);
        // Note: description is just for documentation, not used in the test
    }

    [Fact]
    public void Constructor_ShouldAcceptMaxLengthValues()
    {
        // Arrange
        var reset = new string('a', WorkflowConstants.MaxTimerResetLength);
        var duration = new string('P', WorkflowConstants.MaxDurationLength);

        // Act
        var timerConfig = new TimerConfig(reset, duration);

        // Assert
        Assert.Equal(reset, timerConfig.Reset);
        Assert.Equal(duration, timerConfig.Duration);
    }
}

