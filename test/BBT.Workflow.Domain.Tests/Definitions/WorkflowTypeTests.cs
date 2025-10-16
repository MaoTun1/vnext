using System;
using System.Linq;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Definitions;

public class WorkflowTypeTests
{
    [Fact]
    public void FromCode_ShouldReturnCore_WhenCodeIsC()
    {
        // Arrange & Act
        var result = WorkflowType.FromCode("C");

        // Assert
        Assert.Equal(WorkflowType.Core, result);
        Assert.Equal("C", result.Code);
        Assert.Equal("Core", result.Description);
    }

    [Fact]
    public void FromCode_ShouldReturnFlow_WhenCodeIsF()
    {
        // Arrange & Act
        var result = WorkflowType.FromCode("F");

        // Assert
        Assert.Equal(WorkflowType.Flow, result);
        Assert.Equal("F", result.Code);
        Assert.Equal("Flow", result.Description);
    }

    [Fact]
    public void FromCode_ShouldReturnSubFlow_WhenCodeIsS()
    {
        // Arrange & Act
        var result = WorkflowType.FromCode("S");

        // Assert
        Assert.Equal(WorkflowType.SubFlow, result);
        Assert.Equal("S", result.Code);
        Assert.Equal("Sub Flow", result.Description);
    }

    [Fact]
    public void FromCode_ShouldReturnSubProcess_WhenCodeIsP()
    {
        // Arrange & Act
        var result = WorkflowType.FromCode("P");

        // Assert
        Assert.Equal(WorkflowType.SubProcess, result);
        Assert.Equal("P", result.Code);
        Assert.Equal("Sub Process", result.Description);
    }

    [Fact]
    public void FromCode_ShouldThrowArgumentException_WhenCodeIsInvalid()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => WorkflowType.FromCode("X"));
        Assert.Contains("Unknown workflow type code: X", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("INVALID")]
    public void FromCode_ShouldThrowArgumentException_WhenCodeIsInvalidOrEmpty(string code)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => WorkflowType.FromCode(code));
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingSameType()
    {
        // Arrange
        var type1 = WorkflowType.Core;
        var type2 = WorkflowType.FromCode("C");

        // Act & Assert
        Assert.True(type1.Equals(type2));
        Assert.Equal(type1, type2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingDifferentTypes()
    {
        // Arrange
        var type1 = WorkflowType.Core;
        var type2 = WorkflowType.Flow;

        // Act & Assert
        Assert.False(type1.Equals(type2));
        Assert.NotEqual(type1, type2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingWithNull()
    {
        // Arrange
        var type = WorkflowType.Core;

        // Act & Assert
        Assert.False(type.Equals(null));
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForEqualTypes()
    {
        // Arrange
        var type1 = WorkflowType.Core;
        var type2 = WorkflowType.FromCode("C");

        // Act & Assert
        Assert.Equal(type1.GetHashCode(), type2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferent_ForDifferentTypes()
    {
        // Arrange
        var type1 = WorkflowType.Core;
        var type2 = WorkflowType.Flow;

        // Act & Assert
        Assert.NotEqual(type1.GetHashCode(), type2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnCorrectFormat()
    {
        // Arrange
        var type = WorkflowType.Core;

        // Act
        var result = type.ToString();

        // Assert
        Assert.Equal("Core (C)", result);
    }

    [Theory]
    [InlineData("C", "Core (C)")]
    [InlineData("F", "Flow (F)")]
    [InlineData("S", "Sub Flow (S)")]
    [InlineData("P", "Sub Process (P)")]
    public void ToString_ShouldReturnCorrectFormat_ForAllTypes(string code, string expected)
    {
        // Arrange
        var type = WorkflowType.FromCode(code);

        // Act
        var result = type.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StaticInstances_ShouldBeSingleton()
    {
        // Arrange & Act
        var core1 = WorkflowType.Core;
        var core2 = WorkflowType.Core;

        // Assert
        Assert.Same(core1, core2);
    }

    [Fact]
    public void AllStaticInstances_ShouldBeAvailable()
    {
        // Assert
        Assert.NotNull(WorkflowType.Core);
        Assert.NotNull(WorkflowType.Flow);
        Assert.NotNull(WorkflowType.SubFlow);
        Assert.NotNull(WorkflowType.SubProcess);
    }
}

