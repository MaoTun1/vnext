using System;
using BBT.Workflow.Domain.Shared;
using Xunit;

namespace BBT.Workflow.Domain.Shared;

public class SortDirectionTests
{
    [Fact]
    public void Ascending_ShouldHaveCorrectValue()
    {
        // Act
        var value = SortDirection.Ascending;

        // Assert
        Assert.Equal("asc", value);
    }

    [Fact]
    public void Descending_ShouldHaveCorrectValue()
    {
        // Act
        var value = SortDirection.Descending;

        // Assert
        Assert.Equal("desc", value);
    }

    [Fact]
    public void Ascending_ShouldBeConstant()
    {
        // Arrange & Act
        var value1 = SortDirection.Ascending;
        var value2 = SortDirection.Ascending;

        // Assert
        Assert.Equal(value1, value2);
        Assert.Same(value1, value2);
    }

    [Fact]
    public void Descending_ShouldBeConstant()
    {
        // Arrange & Act
        var value1 = SortDirection.Descending;
        var value2 = SortDirection.Descending;

        // Assert
        Assert.Equal(value1, value2);
        Assert.Same(value1, value2);
    }

    [Fact]
    public void Constants_ShouldBeDifferent()
    {
        // Assert
        Assert.NotEqual(SortDirection.Ascending, SortDirection.Descending);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public void Constants_ShouldBeUsableInComparison(string value)
    {
        // Act
        var isAscending = value == SortDirection.Ascending;
        var isDescending = value == SortDirection.Descending;

        // Assert
        Assert.True(isAscending || isDescending);
    }

    [Fact]
    public void Constants_ShouldBeUsableInSwitch()
    {
        // Arrange
        var direction = SortDirection.Ascending;
        var result = "";

        // Act
        switch (direction)
        {
            case SortDirection.Ascending:
                result = "Ascending";
                break;
            case SortDirection.Descending:
                result = "Descending";
                break;
            default:
                result = "Unknown";
                break;
        }

        // Assert
        Assert.Equal("Ascending", result);
    }

    [Fact]
    public void CanBeUsedAsStringDirectly()
    {
        // Arrange
        var direction = SortDirection.Ascending;

        // Act
        var orderBy = $"name {direction}";

        // Assert
        Assert.Equal("name asc", orderBy);
    }
}

