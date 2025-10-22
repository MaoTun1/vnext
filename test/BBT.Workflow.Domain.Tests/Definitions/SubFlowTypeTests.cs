using System;
using System.Linq;
using BBT.Workflow.Definitions;
using Xunit;

namespace BBT.Workflow.Definitions;

public class SubFlowTypeTests
{
    [Fact]
    public void FromCode_ShouldReturnSubFlow_WhenCodeIsS()
    {
        // Arrange & Act
        var result = SubFlowType.FromCode("S");

        // Assert
        Assert.Equal(SubFlowType.SubFlow, result);
        Assert.Equal("S", result.Code);
        Assert.Equal("Sub Flow", result.Description);
    }

    [Fact]
    public void FromCode_ShouldReturnSubProcess_WhenCodeIsP()
    {
        // Arrange & Act
        var result = SubFlowType.FromCode("P");

        // Assert
        Assert.Equal(SubFlowType.SubProcess, result);
        Assert.Equal("P", result.Code);
        Assert.Equal("Sub Process", result.Description);
    }

    [Fact]
    public void FromCode_ShouldThrowArgumentException_WhenCodeIsInvalid()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => SubFlowType.FromCode("X"));
        Assert.Contains("Unknown workflow type code: X", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("F")]
    [InlineData("C")]
    public void FromCode_ShouldThrowArgumentException_WhenCodeIsNotSubFlowType(string code)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => SubFlowType.FromCode(code));
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingSameType()
    {
        // Arrange
        var type1 = SubFlowType.SubFlow;
        var type2 = SubFlowType.FromCode("S");

        // Act & Assert
        Assert.True(type1.Equals(type2));
        Assert.Equal(type1, type2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingDifferentTypes()
    {
        // Arrange
        var type1 = SubFlowType.SubFlow;
        var type2 = SubFlowType.SubProcess;

        // Act & Assert
        Assert.False(type1.Equals(type2));
        Assert.NotEqual(type1, type2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingWithNull()
    {
        // Arrange
        var type = SubFlowType.SubFlow;

        // Act & Assert
        Assert.False(type.Equals(null));
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForEqualTypes()
    {
        // Arrange
        var type1 = SubFlowType.SubFlow;
        var type2 = SubFlowType.FromCode("S");

        // Act & Assert
        Assert.Equal(type1.GetHashCode(), type2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferent_ForDifferentTypes()
    {
        // Arrange
        var type1 = SubFlowType.SubFlow;
        var type2 = SubFlowType.SubProcess;

        // Act & Assert
        Assert.NotEqual(type1.GetHashCode(), type2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnCorrectFormat()
    {
        // Arrange
        var type = SubFlowType.SubFlow;

        // Act
        var result = type.ToString();

        // Assert
        Assert.Equal("Sub Flow (S)", result);
    }

    [Theory]
    [InlineData("S", "Sub Flow (S)")]
    [InlineData("P", "Sub Process (P)")]
    public void ToString_ShouldReturnCorrectFormat_ForAllTypes(string code, string expected)
    {
        // Arrange
        var type = SubFlowType.FromCode(code);

        // Act
        var result = type.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StaticInstances_ShouldBeSingleton()
    {
        // Arrange & Act
        var subFlow1 = SubFlowType.SubFlow;
        var subFlow2 = SubFlowType.SubFlow;

        // Assert
        Assert.Same(subFlow1, subFlow2);
    }

    [Fact]
    public void AllStaticInstances_ShouldBeAvailable()
    {
        // Assert
        Assert.NotNull(SubFlowType.SubFlow);
        Assert.NotNull(SubFlowType.SubProcess);
    }
}

