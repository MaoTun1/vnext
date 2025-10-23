using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using BBT.Workflow.Shared.Merging;
using Xunit;

namespace BBT.Workflow.Shared.Merging;

public class ExpandoObjectMergeStrategyTests
{
    private readonly ExpandoObjectMergeStrategy _strategy;

    public ExpandoObjectMergeStrategyTests()
    {
        _strategy = new ExpandoObjectMergeStrategy();
    }

    [Fact]
    public void Merge_ShouldMergeProperties_WhenBothAreExpandoObjects()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.Name = "Target";
        target.Age = 30;

        dynamic source = new ExpandoObject();
        source.Age = 35;
        source.City = "New York";

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(3, dict.Count);
        Assert.Equal("Target", dict["Name"]);
        Assert.Equal(35, dict["Age"]);
        Assert.Equal("New York", dict["City"]);
    }

    [Fact]
    public void Merge_ShouldReturnSource_WhenTargetIsNotExpandoObject()
    {
        // Arrange
        var target = "not-expando";
        dynamic source = new ExpandoObject();
        source.Value = 123;

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Same(source, result);
    }

    [Fact]
    public void Merge_ShouldReturnSource_WhenSourceIsNotExpandoObject()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.Value = 123;
        var source = "not-expando";

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Same(source, result);
    }

    [Fact]
    public void Merge_ShouldOverrideSimpleProperties()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.Name = "John";
        target.Age = 25;

        dynamic source = new ExpandoObject();
        source.Name = "Jane";

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal("Jane", dict["Name"]);
        Assert.Equal(25, dict["Age"]);
    }

    [Fact]
    public void Merge_ShouldAddNewProperties()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.Existing = "value";

        dynamic source = new ExpandoObject();
        source.New1 = "value1";
        source.New2 = "value2";

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(3, dict.Count);
        Assert.True(dict.ContainsKey("Existing"));
        Assert.True(dict.ContainsKey("New1"));
        Assert.True(dict.ContainsKey("New2"));
    }

    [Fact]
    public void Merge_ShouldMergeNestedExpandoObjects()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        dynamic targetNested = new ExpandoObject();
        targetNested.Value = 10;
        targetNested.Name = "Original";
        target.Nested = targetNested;

        dynamic source = new ExpandoObject();
        dynamic sourceNested = new ExpandoObject();
        sourceNested.Value = 20;
        sourceNested.NewProp = "Added";
        source.Nested = sourceNested;

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        var nested = dict["Nested"] as IDictionary<string, object?>;
        Assert.NotNull(nested);
        Assert.Equal(3, nested.Count);
        Assert.Equal(20, nested["Value"]);
        Assert.Equal("Original", nested["Name"]);
        Assert.Equal("Added", nested["NewProp"]);
    }

    [Fact]
    public void Merge_ShouldHandleEmptyTarget()
    {
        // Arrange
        var target = new ExpandoObject();

        dynamic source = new ExpandoObject();
        source.Name = "Test";
        source.Value = 123;

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(2, dict.Count);
        Assert.Equal("Test", dict["Name"]);
        Assert.Equal(123, dict["Value"]);
    }

    [Fact]
    public void Merge_ShouldHandleEmptySource()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.Name = "Test";
        target.Value = 123;

        var source = new ExpandoObject();

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(2, dict.Count);
        Assert.Equal("Test", dict["Name"]);
        Assert.Equal(123, dict["Value"]);
    }

    [Fact]
    public void Merge_ShouldModifyTargetInPlace()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.Original = "value";

        dynamic source = new ExpandoObject();
        source.New = "added";

        // Act
        var result = _strategy.Merge(target, source);

        // Assert
        Assert.Same(target, result);
        var targetDict = (IDictionary<string, object?>)target;
        Assert.True(targetDict.ContainsKey("New"));
    }

    [Fact]
    public void Merge_ShouldHandleNullProperties()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.Value = "not-null";

        dynamic source = new ExpandoObject();
        source.Value = null;

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        // ObjectMerger.MergeValues returns target when source is null
        // So Value will keep its original value
        Assert.Equal("not-null", dict["Value"]);
    }

    [Fact]
    public void Merge_ShouldHandleMixedTypes()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        target.String = "text";
        target.Number = 42;
        target.Boolean = true;

        dynamic source = new ExpandoObject();
        source.Number = 100;
        source.Array = new[] { 1, 2, 3 };

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        Assert.Equal(4, dict.Count);
        Assert.Equal("text", dict["String"]);
        Assert.Equal(100, dict["Number"]);
        Assert.True((bool)dict["Boolean"]!);
        Assert.NotNull(dict["Array"]);
    }

    [Fact]
    public void Merge_ShouldHandleDeeplyNestedStructures()
    {
        // Arrange
        dynamic target = new ExpandoObject();
        dynamic level1 = new ExpandoObject();
        dynamic level2 = new ExpandoObject();
        level2.Value = "deep";
        level1.Nested = level2;
        target.Root = level1;

        dynamic source = new ExpandoObject();
        dynamic sLevel1 = new ExpandoObject();
        dynamic sLevel2 = new ExpandoObject();
        sLevel2.NewValue = "added";
        sLevel1.Nested = sLevel2;
        source.Root = sLevel1;

        // Act
        var result = _strategy.Merge(target, source) as ExpandoObject;

        // Assert
        Assert.NotNull(result);
        var dict = (IDictionary<string, object?>)result;
        var root = dict["Root"] as IDictionary<string, object?>;
        Assert.NotNull(root);
        var nested = root["Nested"] as IDictionary<string, object?>;
        Assert.NotNull(nested);
        Assert.Equal(2, nested.Count);
        Assert.True(nested.ContainsKey("Value"));
        Assert.True(nested.ContainsKey("NewValue"));
    }
}

