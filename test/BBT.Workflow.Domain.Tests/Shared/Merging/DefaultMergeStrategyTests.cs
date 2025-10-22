using System;
using BBT.Workflow.Shared.Merging;
using Xunit;

namespace BBT.Workflow.Shared.Merging;

public class DefaultMergeStrategyTests
{
    private readonly DefaultMergeStrategy _strategy;

    public DefaultMergeStrategyTests()
    {
        _strategy = new DefaultMergeStrategy();
    }

    [Fact]
    public void Merge_ShouldReturnSource_WhenBothValuesProvided()
    {
        // Arrange
        var target = "target-value";
        var source = "source-value";

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Equal(source, result);
    }

    [Fact]
    public void Merge_ShouldReturnSource_WhenTargetIsNull()
    {
        // Arrange
        object? target = null;
        var source = "source-value";

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Equal(source, result);
    }

    [Fact]
    public void Merge_ShouldReturnNull_WhenSourceIsNull()
    {
        // Arrange
        var target = "target-value";
        object? source = null;

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Merge_ShouldReturnNull_WhenBothAreNull()
    {
        // Arrange
        object? target = null;
        object? source = null;

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Merge_ShouldWorkWithIntegers()
    {
        // Arrange
        var target = 10;
        var source = 20;

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Equal(source, result);
    }

    [Fact]
    public void Merge_ShouldWorkWithComplexObjects()
    {
        // Arrange
        var target = new { Name = "Target", Value = 1 };
        var source = new { Name = "Source", Value = 2 };

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Same(source, result);
    }

    [Fact]
    public void Merge_ShouldIgnoreTargetValue()
    {
        // Arrange
        var target = new { Important = "data", Keep = "this" };
        var source = new { Different = "value" };

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Same(source, result);
        Assert.NotSame(target, result);
    }

    [Fact]
    public void Merge_ShouldWorkWithStrings()
    {
        // Arrange
        var target = "old-value";
        var source = "new-value";

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Equal("new-value", result);
    }

    [Fact]
    public void Merge_ShouldWorkWithBooleans()
    {
        // Arrange
        var target = true;
        var source = false;

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Equal(false, result);
    }

    [Fact]
    public void Merge_ShouldWorkWithDifferentTypes()
    {
        // Arrange
        var target = 123;
        var source = "string-value";

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Equal(source, result);
        Assert.IsType<string>(result);
    }
}

