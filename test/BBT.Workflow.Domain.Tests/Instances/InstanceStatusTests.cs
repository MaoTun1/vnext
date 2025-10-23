using System;
using Xunit;

namespace BBT.Workflow.Instances;

/// <summary>
/// Unit tests for InstanceStatus
/// </summary>
public class InstanceStatusTests : DomainTestBase<DomainEntryPoint>
{
    [Fact]
    public void StaticInstances_ShouldBeInitialized()
    {
        // Assert
        Assert.NotNull(InstanceStatus.Busy);
        Assert.NotNull(InstanceStatus.Active);
        Assert.NotNull(InstanceStatus.Passive);
        Assert.NotNull(InstanceStatus.Completed);
        Assert.NotNull(InstanceStatus.Faulted);
    }

    [Theory]
    [InlineData("B", "Busy")]
    [InlineData("A", "Active")]
    [InlineData("P", "Passive")]
    [InlineData("C", "Completed")]
    [InlineData("F", "Faulted")]
    public void StaticInstances_ShouldHaveCorrectCodeAndDescription(string expectedCode, string expectedDescription)
    {
        // Arrange
        var status = InstanceStatus.FromCode(expectedCode);

        // Assert
        Assert.Equal(expectedCode, status.Code);
        Assert.Equal(expectedDescription, status.Description);
    }

    [Fact]
    public void FromCode_ShouldReturnBusy_WhenCodeIsB()
    {
        // Act
        var status = InstanceStatus.FromCode("B");

        // Assert
        Assert.Equal(InstanceStatus.Busy, status);
    }

    [Fact]
    public void FromCode_ShouldReturnActive_WhenCodeIsA()
    {
        // Act
        var status = InstanceStatus.FromCode("A");

        // Assert
        Assert.Equal(InstanceStatus.Active, status);
    }

    [Fact]
    public void FromCode_ShouldReturnPassive_WhenCodeIsP()
    {
        // Act
        var status = InstanceStatus.FromCode("P");

        // Assert
        Assert.Equal(InstanceStatus.Passive, status);
    }

    [Fact]
    public void FromCode_ShouldReturnCompleted_WhenCodeIsC()
    {
        // Act
        var status = InstanceStatus.FromCode("C");

        // Assert
        Assert.Equal(InstanceStatus.Completed, status);
    }

    [Fact]
    public void FromCode_ShouldReturnFaulted_WhenCodeIsF()
    {
        // Act
        var status = InstanceStatus.FromCode("F");

        // Assert
        Assert.Equal(InstanceStatus.Faulted, status);
    }

    [Fact]
    public void FromCode_ShouldThrow_WhenCodeIsUnknown()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => InstanceStatus.FromCode("X"));
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenStatusesAreTheSame()
    {
        // Arrange
        var status1 = InstanceStatus.Active;
        var status2 = InstanceStatus.FromCode("A");

        // Act & Assert
        Assert.True(status1.Equals(status2));
        Assert.True(status1 == status2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenStatusesAreDifferent()
    {
        // Arrange
        var status1 = InstanceStatus.Active;
        var status2 = InstanceStatus.Busy;

        // Act & Assert
        Assert.False(status1.Equals(status2));
        Assert.False(status1 == status2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparedWithNull()
    {
        // Arrange
        var status = InstanceStatus.Active;

        // Act & Assert
        Assert.False(status.Equals(null));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparedWithDifferentType()
    {
        // Arrange
        var status = InstanceStatus.Active;
        var other = "Active";

        // Act & Assert
        Assert.False(status.Equals(other));
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent_ForSameStatus()
    {
        // Arrange
        var status1 = InstanceStatus.Active;
        var status2 = InstanceStatus.FromCode("A");

        // Act & Assert
        Assert.Equal(status1.GetHashCode(), status2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferent_ForDifferentStatuses()
    {
        // Arrange
        var status1 = InstanceStatus.Active;
        var status2 = InstanceStatus.Busy;

        // Act & Assert
        Assert.NotEqual(status1.GetHashCode(), status2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var status = InstanceStatus.Active;

        // Act
        var result = status.ToString();

        // Assert
        Assert.Equal("Active (A)", result);
    }

    [Theory]
    [InlineData("B", "Busy (B)")]
    [InlineData("A", "Active (A)")]
    [InlineData("P", "Passive (P)")]
    [InlineData("C", "Completed (C)")]
    [InlineData("F", "Faulted (F)")]
    public void ToString_ShouldReturnCorrectFormat(string code, string expected)
    {
        // Arrange
        var status = InstanceStatus.FromCode(code);

        // Act
        var result = status.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StaticInstances_ShouldBeSingleton()
    {
        // Arrange
        var busy1 = InstanceStatus.Busy;
        var busy2 = InstanceStatus.FromCode("B");

        // Assert
        Assert.Same(busy1, busy2);
    }

    [Fact]
    public void Code_ShouldNotBeNull()
    {
        // Arrange
        var allStatuses = new[]
        {
            InstanceStatus.Busy,
            InstanceStatus.Active,
            InstanceStatus.Passive,
            InstanceStatus.Completed,
            InstanceStatus.Faulted
        };

        // Act & Assert
        foreach (var status in allStatuses)
        {
            Assert.NotNull(status.Code);
            Assert.NotEmpty(status.Code);
        }
    }

    [Fact]
    public void Description_ShouldNotBeNull()
    {
        // Arrange
        var allStatuses = new[]
        {
            InstanceStatus.Busy,
            InstanceStatus.Active,
            InstanceStatus.Passive,
            InstanceStatus.Completed,
            InstanceStatus.Faulted
        };

        // Act & Assert
        foreach (var status in allStatuses)
        {
            Assert.NotNull(status.Description);
            Assert.NotEmpty(status.Description);
        }
    }

    [Fact]
    public void Equals_WithIEquatable_ShouldWorkCorrectly()
    {
        // Arrange
        var status1 = InstanceStatus.Active;
        var status2 = InstanceStatus.Active;
        var status3 = InstanceStatus.Busy;

        // Act & Assert
        Assert.True(((IEquatable<InstanceStatus>)status1).Equals(status2));
        Assert.False(((IEquatable<InstanceStatus>)status1).Equals(status3));
    }
}

